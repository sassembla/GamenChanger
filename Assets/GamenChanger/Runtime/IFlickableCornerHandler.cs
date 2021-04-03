using System;
using UnityEngine;

namespace GamenChangerCore
{
    public interface IFlickableCornerHandler
    {
        // handlers
        void OnFlickRequestFromFlickableCorner(FlickableCorner flickableCorner, ref Corner cornerFromLeft, ref Corner cornerFromRight, ref Corner cornerFromTop, ref Corner cornerFromBottom, FlickDirection plannedFlickDir);

        void TouchOnFlickableCornerDetected(FlickableCorner flickableCorner);

        void FlickableCornerWillAppear(FlickableCorner flickableCorner);
        void FlickableCornerAppearProgress(FlickableCorner flickableCorner, float progress);
        void FlickableCornerWillBack(FlickableCorner flickableCorner);// TODO: 名前をなんとかする
        void FlickableCornerWillCancel(FlickableCorner flickableCorner);// TODO: 名前をなんとかする
        void FlickableCornerAppearCancelled(FlickableCorner flickableCorner);
        void FlickableCornerDidAppear(FlickableCorner flickableCorner);

        void FlickableCornerWillDisappear(FlickableCorner flickableCorner);
        void FlickableCornerDisppearProgress(FlickableCorner flickableCorner, float progress);
        void FlickableCornerDisppearCancelled(FlickableCorner flickableCorner);
        void FlickableCornerDidDisappear(FlickableCorner flickableCorner);


        // animations

        void OnFlickProcessAnimationRequired(FlickableCorner flickableCorner, Vector2 targetPosition, Action onDone, Action onCancelled);
        void OnFlickCancelAnimationRequired(FlickableCorner flickableCorner, Vector2 initialPosition, Action onDone);

    }
}