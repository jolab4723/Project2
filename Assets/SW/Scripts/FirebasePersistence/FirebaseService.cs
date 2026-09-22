using System;
using System.IO;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

namespace Core
{
    public enum FirebaseServiceState
    {
        NotInitialized,
        Initializing,
        Ready,
        MissingConfiguration,
        Unavailable
    }

    public sealed class FirebaseInitializationResult
    {
        public bool IsSuccess { get; }
        public FirebaseServiceState State { get; }
        public string Message { get; }

        private FirebaseInitializationResult(
            bool isSuccess,
            FirebaseServiceState state,
            string message)
        {
            IsSuccess = isSuccess;
            State = state;
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// Firebase가 정상적으로 준비된 결과를 만듭니다.
        /// </summary>
        public static FirebaseInitializationResult Success()
        {
            return new FirebaseInitializationResult(
                true,
                FirebaseServiceState.Ready,
                string.Empty);
        }

        /// <summary>
        /// Firebase를 준비하지 못한 상태와 사용자에게 보여 줄 설명을 만듭니다.
        /// </summary>
        public static FirebaseInitializationResult Failure(
            FirebaseServiceState state,
            string message)
        {
            return new FirebaseInitializationResult(false, state, message);
        }
    }

    public sealed class FirebaseLoginResult
    {
        public bool IsSuccess { get; }
        public string UserId { get; }
        public string Message { get; }

        private FirebaseLoginResult(bool isSuccess, string userId, string message)
        {
            IsSuccess = isSuccess;
            UserId = userId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>
        /// Firebase 인증에 성공한 사용자 ID를 담은 결과를 만듭니다.
        /// </summary>
        public static FirebaseLoginResult Success(string userId)
        {
            return new FirebaseLoginResult(true, userId, string.Empty);
        }

        /// <summary>
        /// Firebase 인증에 실패한 이유를 담은 결과를 만듭니다.
        /// </summary>
        public static FirebaseLoginResult Failure(string message)
        {
            return new FirebaseLoginResult(false, string.Empty, message);
        }
    }

    public sealed class FirebaseService
    {
        private static readonly FirebaseService DefaultInstance = new FirebaseService();

        private readonly object initializationLock = new object();
        private Task<FirebaseInitializationResult> initializationTask;

        public static FirebaseService Default => DefaultInstance;
        public FirebaseServiceState State { get; private set; } = FirebaseServiceState.NotInitialized;
        public FirebaseApp App { get; private set; }
        public FirebaseAuth Auth { get; private set; }
        public FirebaseFirestore Firestore { get; private set; }
        public string CurrentUserId => Auth?.CurrentUser?.UserId ?? string.Empty;
        public bool IsSignedIn => !string.IsNullOrEmpty(CurrentUserId);

        private FirebaseService()
        {
        }

        /// <summary>
        /// Firebase 의존성을 한 번만 확인하고 App, Auth, Firestore 사용 준비를 완료합니다.
        /// </summary>
        public Task<FirebaseInitializationResult> InitializeAsync()
        {
            lock (initializationLock)
            {
                if (initializationTask == null ||
                    initializationTask.IsFaulted ||
                    initializationTask.IsCanceled)
                {
                    initializationTask = InitializeInternalAsync();
                }

                return initializationTask;
            }
        }

        /// <summary>
        /// 이메일과 비밀번호로 로그인하고 성공한 Firebase 사용자 ID를 반환합니다.
        /// </summary>
        public async Task<FirebaseLoginResult> SignInWithEmailAndPasswordAsync(
            string email,
            string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                return FirebaseLoginResult.Failure("이메일과 비밀번호를 입력해 주세요.");
            }

            FirebaseInitializationResult initialization = await InitializeAsync();
            if (!initialization.IsSuccess)
            {
                return FirebaseLoginResult.Failure(initialization.Message);
            }

            try
            {
                AuthResult result = await Auth.SignInWithEmailAndPasswordAsync(
                    email.Trim(),
                    password);
                if (result?.User == null)
                {
                    return FirebaseLoginResult.Failure("로그인 결과에서 사용자 정보를 확인하지 못했습니다.");
                }

                return FirebaseLoginResult.Success(result.User.UserId);
            }
            catch (FirebaseException exception)
            {
                Debug.LogWarning(
                    $"[FirebaseService] 로그인 실패. ErrorCode={exception.ErrorCode}, Message={exception.Message}");
                return FirebaseLoginResult.Failure(
                    "로그인에 실패했습니다.\n이메일, 비밀번호와 Firebase 인증 설정을 확인해 주세요.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FirebaseService] 로그인 중 예외 발생: {exception}");
                return FirebaseLoginResult.Failure("Firebase 로그인 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 이메일과 비밀번호로 Firebase 사용자를 만들고 생성된 사용자 ID를 반환합니다.
        /// </summary>
        public async Task<FirebaseLoginResult> CreateUserWithEmailAndPasswordAsync(
            string email,
            string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                return FirebaseLoginResult.Failure("이메일과 비밀번호를 입력해 주세요.");
            }

            FirebaseInitializationResult initialization = await InitializeAsync();
            if (!initialization.IsSuccess)
            {
                return FirebaseLoginResult.Failure(initialization.Message);
            }

            try
            {
                AuthResult result = await Auth.CreateUserWithEmailAndPasswordAsync(
                    email.Trim(),
                    password);
                if (result?.User == null)
                {
                    return FirebaseLoginResult.Failure(
                        "계정 생성 결과에서 사용자 정보를 확인하지 못했습니다.");
                }

                return FirebaseLoginResult.Success(result.User.UserId);
            }
            catch (FirebaseException exception)
            {
                Debug.LogWarning(
                    $"[FirebaseService] 계정 생성 실패. ErrorCode={exception.ErrorCode}, Message={exception.Message}");
                return FirebaseLoginResult.Failure(
                    GetAccountCreationErrorMessage(exception));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FirebaseService] 계정 생성 중 예외 발생: {exception}");
                return FirebaseLoginResult.Failure(
                    "Firebase 계정 생성 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 현재 Firebase 사용자 세션을 로그아웃합니다.
        /// </summary>
        public void SignOut()
        {
            Auth?.SignOut();
        }

        /// <summary>
        /// Firebase 인증 오류 코드를 계정 생성 화면에서 이해하기 쉬운 안내 문구로 바꿉니다.
        /// </summary>
        private static string GetAccountCreationErrorMessage(FirebaseException exception)
        {
            switch ((AuthError)exception.ErrorCode)
            {
                case AuthError.EmailAlreadyInUse:
                    return "이미 사용 중인 이메일입니다.";
                case AuthError.InvalidEmail:
                case AuthError.MissingEmail:
                    return "올바른 이메일 주소를 입력해 주세요.";
                case AuthError.WeakPassword:
                case AuthError.MissingPassword:
                    return "Firebase 비밀번호 정책을 만족하는 비밀번호를 입력해 주세요.";
                case AuthError.OperationNotAllowed:
                    return "Firebase Console에서 이메일/비밀번호 로그인을 활성화해 주세요.";
                case AuthError.NetworkRequestFailed:
                    return "네트워크 연결을 확인한 뒤 다시 시도해 주세요.";
                case AuthError.TooManyRequests:
                    return "요청이 너무 많습니다. 잠시 후 다시 시도해 주세요.";
                default:
                    return "계정을 생성하지 못했습니다. 입력값과 Firebase 인증 설정을 확인해 주세요.";
            }
        }

        /// <summary>
        /// 설정 파일과 SDK 의존성을 확인한 뒤 필요한 Firebase 인스턴스를 보관합니다.
        /// </summary>
        private async Task<FirebaseInitializationResult> InitializeInternalAsync()
        {
            State = FirebaseServiceState.Initializing;

#if UNITY_EDITOR
            if (!HasEditorConfigurationFile())
            {
                State = FirebaseServiceState.MissingConfiguration;
                return FirebaseInitializationResult.Failure(
                    State,
                    "Firebase 설정 파일이 없습니다. google-services.json 또는 GoogleService-Info.plist를 Assets 폴더에 추가해 주세요.");
            }
#endif

            try
            {
                DependencyStatus dependencyStatus =
                    await FirebaseApp.CheckAndFixDependenciesAsync();
                if (dependencyStatus != DependencyStatus.Available)
                {
                    State = FirebaseServiceState.Unavailable;
                    return FirebaseInitializationResult.Failure(
                        State,
                        $"Firebase 의존성을 준비하지 못했습니다: {dependencyStatus}");
                }

                App = FirebaseApp.DefaultInstance;
                Auth = FirebaseAuth.DefaultInstance;
                Firestore = FirebaseFirestore.DefaultInstance;
                State = FirebaseServiceState.Ready;
                return FirebaseInitializationResult.Success();
            }
            catch (Exception exception)
            {
                State = FirebaseServiceState.Unavailable;
                Debug.LogError($"[FirebaseService] 초기화 실패: {exception}");
                return FirebaseInitializationResult.Failure(
                    State,
                    "Firebase 초기화에 실패했습니다. 설정 파일과 프로젝트 연결 상태를 확인해 주세요.");
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Unity Editor에서 사용할 모바일 또는 자동 생성된 데스크톱 설정 파일이 있는지 확인합니다.
        /// </summary>
        private static bool HasEditorConfigurationFile()
        {
            string assetsPath = Application.dataPath;
            return File.Exists(Path.Combine(assetsPath, "google-services.json")) ||
                   File.Exists(Path.Combine(assetsPath, "GoogleService-Info.plist")) ||
                   File.Exists(Path.Combine(
                       assetsPath,
                       "StreamingAssets",
                       "google-services-desktop.json"));
        }
#endif
    }
}
