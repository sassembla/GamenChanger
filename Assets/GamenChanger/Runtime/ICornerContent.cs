namespace GamenChangerCore
{
    public interface ICornerContent
    {
        void Touch();

        void WillAppear();
        void AppearProgress(float progress);
        void AppearCancelled();
        void DidAppear();

        void WillDisappear();
        void DisppearProgress(float progress);
        void DisppearCancelled();
        void DidDisappear();
    }
}