param(
    [Parameter(Mandatory = $true)][string]$OrmPath,
    [Parameter(Mandatory = $true)][string]$MetallicSmoothnessPath,
    [Parameter(Mandatory = $true)][string]$OcclusionPath
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class OrmUnityPacker
{
    public static void Pack(string ormPath, string metallicSmoothnessPath, string occlusionPath)
    {
        using (var sourceFile = new Bitmap(ormPath))
        using (var source = new Bitmap(sourceFile.Width, sourceFile.Height, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(source))
        {
            graphics.DrawImageUnscaled(sourceFile, 0, 0);
            var rect = new Rectangle(0, 0, source.Width, source.Height);
            var sourceData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var sourceBytes = new byte[Math.Abs(sourceData.Stride) * source.Height];
            Marshal.Copy(sourceData.Scan0, sourceBytes, 0, sourceBytes.Length);
            source.UnlockBits(sourceData);

            using (var metallic = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            using (var occlusion = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            {
                var metallicBytes = new byte[sourceBytes.Length];
                var occlusionBytes = new byte[sourceBytes.Length];
                for (var y = 0; y < source.Height; y++)
                {
                    for (var x = 0; x < source.Width; x++)
                    {
                        var i = y * sourceData.Stride + x * 4;
                        var metallicValue = sourceBytes[i];      // glTF ORM B
                        var roughness = sourceBytes[i + 1];     // glTF ORM G
                        var ao = sourceBytes[i + 2];            // glTF ORM R

                        metallicBytes[i] = 0;
                        metallicBytes[i + 1] = 0;
                        metallicBytes[i + 2] = metallicValue;
                        metallicBytes[i + 3] = (byte)(255 - roughness);

                        occlusionBytes[i] = ao;
                        occlusionBytes[i + 1] = ao;
                        occlusionBytes[i + 2] = ao;
                        occlusionBytes[i + 3] = 255;
                    }
                }

                WriteBitmap(metallic, metallicBytes, rect, metallicSmoothnessPath);
                WriteBitmap(occlusion, occlusionBytes, rect, occlusionPath);
            }
        }
    }

    private static void WriteBitmap(Bitmap bitmap, byte[] bytes, Rectangle rect, string path)
    {
        var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        bitmap.UnlockBits(data);
        bitmap.Save(path, ImageFormat.Png);
    }
}
'@

[OrmUnityPacker]::Pack($OrmPath, $MetallicSmoothnessPath, $OcclusionPath)
