using UnityEngine;

namespace FateWeaver.Unity
{
    internal static class GeneratedCardChildren
    {
        internal static void Clear(RectTransform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                var child = parent.GetChild(index).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
        }
    }
}
