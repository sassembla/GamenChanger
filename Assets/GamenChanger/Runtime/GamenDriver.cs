using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace GamenChangerCore
{
    public class GamenDriver
    {
        private IEnumerator cor;

        private enum DriveDirection
        {
            NONE,
            RIGHT,
            LEFT,
            UP,
            DOWN
        }

        public GamenDriver(RectTransform fromRectTransform, Vector2 targetPosition, Action<RectTransform> onDriven)
        {
            state = DriveState.Driving;
            /*
                存在するのは
                1.Corner
                2.FlickableCorner
                3.OneOfNCorner
                4.DraggableCorner
                5.OverlayCorner
                の5種で、連続できるのは上2つくらいだなー。まあなんでもCornerの特性を持つので動かして持ってくることは可能。

                どんなパターンが存在するかを調べる。
                まずサポートできるのは、
                ・Draggable
                この1パターン。
            */
            this.cor = DraggableDrive(fromRectTransform, targetPosition, onDriven);
        }

        public GamenDriver(Corner[] steps)
        {
            // ここでプランを作る。それをmoveNextがgeneratorとして実行していく。

            if (steps.Length == 0)
            {
                return;
            }

            state = DriveState.Driving;

            /*
                存在するのは
                1.Corner
                2.FlickableCorner
                3.OneOfNCorner
                4.DraggableCorner
                5.OverlayCorner
                の5種で、連続できるのは上2つくらいだなー。まあなんでもCornerの特性を持つので動かして持ってくることは可能。

                どんなパターンが存在するかを調べる。
                まずサポートできるのは、
                ・全てがFlickable
                ・Flickableから他のCorner
                ・Draggable(ただしindexオンリー)
                この2パターン。
            */

            if (steps[0] is FlickableCorner)
            {
                // pass.
            }
            else
            {
                throw new Exception("unsupported drive pattern. first step must be FlickableCorner.");
            }

            // 全てのステップがFlickableCornerでできている
            if (!steps.Where(s => !(s is FlickableCorner)).Any())
            {
                var flickableSteps = steps.Select(step => (FlickableCorner)step).ToArray();
                this.cor = FlickableDrive(flickableSteps);
                return;
            }

            throw new Exception("unsupported pattern.");
        }

        private IEnumerator DraggableDrive(RectTransform draggableTargetContentRectTrans, Vector2 targetPositionSource, Action<RectTransform> onDriven)
        {
            var maxCount = 10;// TODO: frameなので、そのまま使うと厄介。
            var driveDivide = 0.3f;

            IEnumerator cor()
            {
                Vector3 targetPosition = targetPositionSource;
                Vector3 origin = targetPosition;

                var count = 0;

                var totalMove = Vector3.zero;

                // このcorの中では一貫してpositionを使っている。
                var startPosition = draggableTargetContentRectTrans.position;

                while (true)
                {
                    // このフレームでの移動距離を出す
                    var move = origin * driveDivide;
                    totalMove += move;

                    // 移動させる
                    draggableTargetContentRectTrans.position = startPosition + totalMove;

                    // オリジナルを減らす
                    origin = origin - move;

                    count++;
                    if (count == maxCount)
                    {
                        // 終了するので位置をジャストにする
                        draggableTargetContentRectTrans.position = startPosition + targetPosition;

                        // Draggableへと通知
                        onDriven(draggableTargetContentRectTrans);

                        yield break;
                    }

                    yield return null;
                }
            };

            return cor();
        }

        private IEnumerator FlickableDrive(FlickableCorner[] flickableSteps)
        {
            var totalDriveLength = 0f;

            // TODO: 現在はシンプルな1方向への移動しかサポートしないが、そのうちN回曲がる、などもサポートしてみたいところではある。カカカッて曲がるの。
            var dir = DriveDirection.NONE;
            if (flickableSteps[0].CornerFromRight == flickableSteps[1])
            {
                dir = DriveDirection.RIGHT;

                // 右に行く場合、最後にtoが入っているので、その分移動する必要がないので除外する
                var stepList = flickableSteps.ToList();
                stepList.Remove(stepList.Last());
                totalDriveLength = -stepList.Sum(step => ((FlickableCorner)step).MoveUnitSize);
            }
            else if (flickableSteps[0].CornerFromLeft == flickableSteps[1])
            {
                dir = DriveDirection.LEFT;

                // 左に行く場合、現在の原点から最初の画面の移動幅を減らす必要がないので除外する。
                var stepList = flickableSteps.ToList();
                stepList.Remove(stepList[0]);
                totalDriveLength = stepList.Sum(step => ((FlickableCorner)step).MoveUnitSize);
            }
            else if (flickableSteps[0].CornerFromBottom == flickableSteps[1])
            {
                dir = DriveDirection.DOWN;
                Debug.LogError("unsupported yet.");
            }
            else if (flickableSteps[0].CornerFromTop == flickableSteps[1])
            {
                dir = DriveDirection.UP;
                Debug.LogError("unsupported yet.");
            }

            // 方向が決定できなかった
            if (dir == DriveDirection.NONE)
            {
                throw new Exception("direction is unhandled.");
            }

            // TODO: この辺のパラメータも渡せるようにしたいところ。今は内部実装オンリーなので、さてどうするか。対応するflickableのanimation実装があるといいかもなあ。
            var maxCount = 10;// TODO: frameなので、そのまま使うと厄介。
            var driveDivide = 0.3f;


            // 関連する全てのFlickableCornerと関連CornerのrectTransformを収集する。
            var relatedAllFlickableCornerRectTransforms = flickableSteps[0].CollectRelatedFlickableCorners();

            // 開始位置を収集
            // このcorの中では一貫してpositionを使っている。
            var startPositions = relatedAllFlickableCornerRectTransforms.Select(f => new Vector2(f.position.x, f.position.y)).ToArray();

            IEnumerator cor()
            {
                var origin = totalDriveLength;

                var count = 0;

                var totalMove = 0f;

                while (true)
                {
                    // このフレームでの移動距離を出す
                    var move = origin * driveDivide;
                    totalMove += move;

                    // 移動させる
                    for (var j = 0; j < relatedAllFlickableCornerRectTransforms.Length; j++)
                    {
                        var trans = relatedAllFlickableCornerRectTransforms[j];
                        trans.position = startPositions[j] + new Vector2(totalMove, 0);
                    }

                    // オリジナルを減らす
                    origin = origin - move;

                    count++;
                    if (count == maxCount)
                    {
                        // 終了するので位置をジャストにする
                        for (var j = 0; j < relatedAllFlickableCornerRectTransforms.Length; j++)
                        {
                            var trans = relatedAllFlickableCornerRectTransforms[j];
                            trans.position = startPositions[j] + new Vector2(totalDriveLength, 0);
                        }
                        // TODO: onDrivenを実行する必要がある。
                        yield break;
                    }

                    yield return null;
                }
            };

            return cor();
        }

        private enum DriveState
        {
            None,
            Driving,
            Stopping,
            Stopped
        }
        private DriveState state;

        public void Stop()
        {
            // 走行中であれば停止させる
            switch (state)
            {
                case DriveState.Driving:
                    state = DriveState.Stopped;
                    break;
                default:
                    // do nothing.
                    break;
            }
        }

        public bool MoveNext()
        {
            switch (state)
            {
                case DriveState.Driving:
                    // pass.
                    break;
                case DriveState.None:
                    Debug.LogError("invalid state, None");
                    return false;
                case DriveState.Stopping:
                    state = DriveState.Stopped;
                    return false;
                default:
                    Debug.LogError("unhandled state:" + state);
                    return false;
            }

            if (cor == null)
            {
                return false;
            }

            return cor.MoveNext();
        }
    }
}