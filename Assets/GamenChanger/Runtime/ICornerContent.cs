namespace GamenChangerCore
{
    // TODO: なんか名前が気に食わない、CornerなんとかHandlerだろうな
    public interface ICornerContent
    {
        void CornerTouchDetected();

        void CornerAppearProgress(float progress);

        void CornerWillAppear();
        void CornerWillCancel();// TODO: 名前をなんとかしたい
        void CornerWillBack();// TODO: 名前をなんとかしたい
        void CornerAppearCancelled();
        void CornerDidAppear();

        void CornerWillDisappear();
        void CornerDisppearProgress(float progress);
        void CornerDisppearCancelled();
        void CornerDidDisappear();
    }
}