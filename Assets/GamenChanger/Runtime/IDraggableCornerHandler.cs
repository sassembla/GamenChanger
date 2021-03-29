using UnityEngine;

namespace GamenChangerCore
{
    public interface IDraggableCornerHandler
    {
        // グリッドを返すと、drag対象がそのグリッドに応じた動きをするようになる。
        Vector2[] OnInitialized();

        // dragしているオブジェクトがどのグリッドに一番接近しているか通知する。
        void OnDragApproachingToGrid(int index, GameObject go);

        // dragしているオブジェクトがどのグリッドに接地したか通知する。
        void OnGrid(int index, GameObject go);
    }
}