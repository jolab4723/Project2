using System;
using System.Collections.Generic;
using System.Globalization;
using Mirror;

public enum ChatKind : byte { Chat, Acquisition, Warning, Connection }

public readonly struct ChatEntry
{
    public readonly ChatKind Kind;
    public readonly string Text;
    public ChatEntry(ChatKind kind, string text) { Kind = kind; Text = text; }
}

public struct ChatRequest : NetworkMessage
{
    public uint RequestId;
    public string Text;
}

public struct ChatDelivery : NetworkMessage
{
    public uint RequestId;
    public int PlayerNumber;
    public string Text;
    public bool IsOwn;
    public bool Rejected;
}

/// <summary>기존 NetworkManager가 소유하는 세션 기록. UI나 플레이어의 수명에 묶이지 않는다.</summary>
public sealed class ChatSession
{
    public const int HistoryLimit = 200;
    public const int MaxTextLength = 200;
    private readonly List<ChatEntry> entries = new();
    private readonly Dictionary<int, ChatSendWindow> sendWindows = new();
    private readonly Dictionary<int, int> playerNumbers = new();
    private readonly HashSet<ulong> completedRequests = new();
    private readonly Queue<ulong> completedOrder = new();
    private Func<NetworkConnectionToClient, bool> isApproved;
    private PlayerInventorySync_MirrorTest inventory;
    private InventoryController localInventory;
    private NetworkShopPlayerState_MirrorTest shop;
    private MirrorSpawnedPlayerBinder inputBinder;
    private int lastInputFrame = -1;
    private uint nextRequestId;
    private uint pendingRequestId;
    private string pendingDraft;
    private int nextPlayerNumber;

    public IReadOnlyList<ChatEntry> Entries => entries;
    public long Revision { get; private set; }
    public string Draft { get; set; } = string.Empty;
    public bool IsConnected { get; private set; }
    public bool InputFocused { get; private set; }
    public bool ConsumesInputThisFrame => InputFocused || lastInputFrame == UnityEngine.Time.frameCount;
    public bool IsSending => pendingRequestId != 0;
    public bool CanSend => IsConnected && NetworkClient.isConnected && !IsSending;
    public event Action Changed;

    public void StartServer(Func<NetworkConnectionToClient, bool> approved)
    {
        isApproved = approved;
        sendWindows.Clear();
        playerNumbers.Clear();
        nextPlayerNumber = 0;
        NetworkServer.RegisterHandler<ChatRequest>(HandleRequest);
    }

    public void StopServer()
    {
        NetworkServer.UnregisterHandler<ChatRequest>();
        sendWindows.Clear();
        playerNumbers.Clear();
        isApproved = null;
    }

    public void ForgetConnection(int connectionId)
    {
        sendWindows.Remove(connectionId);
        playerNumbers.Remove(connectionId);
    }

    public void StartClient()
    {
        BindLocalPlayer(null);
        entries.Clear();
        Revision++;
        completedRequests.Clear();
        completedOrder.Clear();
        Draft = string.Empty;
        pendingRequestId = nextRequestId = 0;
        IsConnected = false;
        NetworkClient.RegisterHandler<ChatDelivery>(HandleDelivery);
        Changed?.Invoke();
    }

    public void ConfirmConnection()
    {
        if (IsConnected) return;
        IsConnected = true;
        Append(ChatKind.Connection, "세션 채팅에 연결되었습니다.");
    }

    public void StopClient()
    {
        SetInputFocused(false);
        NetworkClient.UnregisterHandler<ChatDelivery>();
        BindLocalPlayer(null);
        bool wasConnected = IsConnected;
        IsConnected = false;
        pendingRequestId = 0;
        if (wasConnected) Append(ChatKind.Connection, "서버와 연결이 끊어졌습니다.");
        else Changed?.Invoke();
    }

    public bool TrySend(string text)
    {
        Draft = text ?? string.Empty;
        if (!CanSend) return false;
        if (!TryValidateText(text, out string clean, out string reason))
        {
            Append(ChatKind.Warning, reason);
            return false;
        }
        pendingRequestId = ++nextRequestId;
        if (pendingRequestId == 0) pendingRequestId = ++nextRequestId;
        pendingDraft = Draft;
        NetworkClient.Send(new ChatRequest { RequestId = pendingRequestId, Text = clean });
        Changed?.Invoke();
        return true;
    }

    public static bool TryValidateText(string text, out string clean, out string reason)
    {
        clean = string.Empty;
        reason = "메시지를 입력해 주세요.";
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.Length > MaxTextLength) { reason = "메시지는 200자까지 입력할 수 있습니다."; return false; }
        for (int i = 0; i < text.Length; i++)
        {
            UnicodeCategory category = char.GetUnicodeCategory(text, i);
            if (char.IsControl(text[i]) || category == UnicodeCategory.Format ||
                category == UnicodeCategory.LineSeparator || category == UnicodeCategory.ParagraphSeparator ||
                (char.IsSurrogate(text[i]) && !char.IsSurrogatePair(text, i)))
            { reason = "줄바꿈이나 제어문자는 사용할 수 없습니다."; return false; }
            if (char.IsHighSurrogate(text[i])) i++;
        }
        clean = text.Trim();
        reason = null;
        return true;
    }

    private void HandleRequest(NetworkConnectionToClient sender, ChatRequest request)
    {
        if (sender == null || !sender.isAuthenticated || isApproved?.Invoke(sender) != true)
            return;
        if (!sendWindows.TryGetValue(sender.connectionId, out ChatSendWindow window))
            sendWindows.Add(sender.connectionId, window = new ChatSendWindow());
        // 중복 요청과 잘못된 본문도 전송 제한에 포함한다.
        if (!window.TryAccept(request.RequestId, NetworkTime.localTime))
        { Reject(sender, request.RequestId, "잠시 후 다시 전송해 주세요."); return; }
        if (!TryValidateText(request.Text, out string text, out string reason))
        { Reject(sender, request.RequestId, reason); return; }
        if (!playerNumbers.TryGetValue(sender.connectionId, out int number))
            playerNumbers.Add(sender.connectionId, number = ++nextPlayerNumber);
        foreach (NetworkConnectionToClient receiver in NetworkServer.connections.Values)
        {
            if (!receiver.isAuthenticated || isApproved?.Invoke(receiver) != true) continue;
            receiver.Send(new ChatDelivery
            {
                RequestId = request.RequestId, PlayerNumber = number, Text = text,
                IsOwn = receiver == sender,
            });
        }
    }

    private static void Reject(NetworkConnectionToClient sender, uint requestId, string reason)
    {
        sender.Send(new ChatDelivery { RequestId = requestId, IsOwn = true, Rejected = true, Text = reason });
    }

    private void HandleDelivery(ChatDelivery message)
    {
        if (message.IsOwn && message.RequestId == pendingRequestId)
        {
            pendingRequestId = 0;
            if (!message.Rejected && Draft == pendingDraft) Draft = string.Empty;
        }
        Append(message.Rejected ? ChatKind.Warning : ChatKind.Chat,
            message.Rejected ? message.Text : $"플레이어 {message.PlayerNumber}: {message.Text}");
    }

    public void BindLocalPlayer(PlayerContext context)
    {
        if (inputBinder != null) inputBinder.SetTextInputBlocked(false);
        if (inventory != null) inventory.RequestCompleted -= HandleInventory;
        if (localInventory != null) localInventory.EquipmentRejected -= HandleLocalEquipmentRejection;
        if (shop != null) shop.RequestCompleted -= HandleShop;
        inventory = context != null ? context.GetComponent<PlayerInventorySync_MirrorTest>() : null;
        localInventory = context != null ? context.Inventory : null;
        shop = context != null ? context.GetComponent<NetworkShopPlayerState_MirrorTest>() : null;
        inputBinder = context != null ? context.GetComponent<MirrorSpawnedPlayerBinder>() : null;
        if (inputBinder != null) inputBinder.SetTextInputBlocked(InputFocused);
        completedRequests.Clear();
        completedOrder.Clear();
        if (inventory != null) inventory.RequestCompleted += HandleInventory;
        if (localInventory != null) localInventory.EquipmentRejected += HandleLocalEquipmentRejection;
        if (shop != null) shop.RequestCompleted += HandleShop;
    }

    public void SetInputFocused(bool focused)
    {
        InputFocused = focused;
        lastInputFrame = UnityEngine.Time.frameCount;
        if (inputBinder != null) inputBinder.SetTextInputBlocked(focused);
    }

    private void HandleInventory(MirrorTestInventoryRequestCompleted result)
    {
        if (inventory == null || !inventory.isLocalPlayer ||
            !RememberRequest(result.RequestId, false)) return;
        if (ChatMessageMapper.TryMap(result, out ChatEntry entry)) Append(entry.Kind, entry.Text);
    }

    private void HandleLocalEquipmentRejection(EquipResult result)
    {
        // 이 경로는 UI에서 거절되어 네트워크 요청 자체가 생기지 않은 경우뿐이다.
        if (inventory != null && inventory.isLocalPlayer && IsConnected)
            Append(ChatKind.Warning, EquipMessageMapper.GetMessage(result));
    }

    private void HandleShop(MirrorTestShopRequestCompleted result)
    {
        if (shop == null || !shop.isLocalPlayer || !RememberRequest(result.RequestId, true)) return;
        if (ChatMessageMapper.TryMap(result, out ChatEntry entry)) Append(entry.Kind, entry.Text);
    }

    internal bool RememberRequest(uint requestId, bool isShop)
    {
        ulong key = ((ulong)(isShop ? 1u : 0u) << 32) | requestId;
        if (requestId == 0 || !completedRequests.Add(key)) return false;
        completedOrder.Enqueue(key);
        while (completedOrder.Count > HistoryLimit) completedRequests.Remove(completedOrder.Dequeue());
        return true;
    }

    internal void Append(ChatKind kind, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (entries.Count == HistoryLimit) entries.RemoveAt(0);
        entries.Add(new ChatEntry(kind, text));
        Revision++;
        Changed?.Invoke();
    }
}

internal sealed class ChatSendWindow
{
    private readonly Queue<double> attempts = new();
    private uint lastRequest;
    public bool TryAccept(uint requestId, double now)
    {
        while (attempts.Count > 0 && now - attempts.Peek() >= 5) attempts.Dequeue();
        if (attempts.Count >= 3) return false;
        attempts.Enqueue(now);
        if (requestId == 0 || requestId <= lastRequest) return false;
        lastRequest = requestId;
        return true;
    }
}
