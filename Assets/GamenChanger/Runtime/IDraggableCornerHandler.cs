using System;
using System.Collections;
using UnityEngine;

namespace GamenChangerCore
{
    public interface IDraggableCornerHandler
    {
        // グリッドを返すと、drag対象がそのグリッドに応じた動きをするようになる。
        Vector2[] OnDraggableCornerInitialized(Func<int, int, GamenDriver> draggableDriver);

        // dragしているオブジェクトがどのグリッドに一番接近しているか通知する。
        void OnDragApproachingToGrid(int index, GameObject go);

        // dragしているオブジェクトがどのグリッドに接地したか通知する。
        void OnDragDoneOnGrid(int index, GameObject go);

        // cancelが発生したことを通知する。
        void OnDragCancelled(int index, GameObject go);

        // アニメーション系

        void OnDragApproachAnimationRequired(int index, GameObject go, Vector2 approachTargetPosition, Action onDone, Action onCancelled);
        void OnDragCancelAnimationRequired(GameObject go, Vector2 initialPosition, Action onDone);
    }
}