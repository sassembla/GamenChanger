using System;
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

        // cancelが発生したことを通知する。
        void OnCancelled(int index, GameObject go);

        // アニメーション系

        void OnApproachAnimationRequired(int index, GameObject go, Vector2 approachTargetPosition, Action onDone, Action onCancelled);
        void OnCancelAnimationRequired(GameObject go, Vector2 initialPosition, Action onDone);
    }
}