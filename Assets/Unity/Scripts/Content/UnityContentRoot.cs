using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>StreamingAssets 아래 콘텐츠 루트. 경로 상수는 여기 하나뿐이고 나머지는 폴더
    /// 스캔이다(규칙 2·3, 설계 §4.5).</summary>
    public static class UnityContentRoot
    {
        private const string FolderName = "Content";

        public static string Path
            => System.IO.Path.Combine(Application.streamingAssetsPath, FolderName);
    }
}
