namespace GamenChangerCore
{
    public interface IFlickableCornerHandler
    {
        void OnFlickRequestFromFlickableCorner(FlickableCorner flickableCorner, ref Corner cornerFromLeft, ref Corner cornerFromRight, ref Corner cornerFromTop, ref Corner cornerFromBottom, FlickDirection plannedFlickDir);

        void Touch(FlickableCorner flickableCorner);

        void WillAppear(FlickableCorner flickableCorner);
        void AppearProgress(FlickableCorner flickableCorner, float progress);
        void AppearCancelled(FlickableCorner flickableCorner);
        void DidAppear(FlickableCorner flickableCorner);

        void WillDisappear(FlickableCorner flickableCorner);
        void DisppearProgress(FlickableCorner flickableCorner, float progress);
        void DisppearCancelled(FlickableCorner flickableCorner);
        void DidDisappear(FlickableCorner flickableCorner);

    }
}