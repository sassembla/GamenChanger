using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamenChangerCore
{
    // TODO: 複数のdraggableどうしが重なるのを許すかどうか -> 需要が見合ってないのでやらない
    // TODO: 既にgridに要素があったらreject？ -> 難しそうだからやらない
    public class DraggableCorner : Corner
    {
        public bool ConstraintHorizontal = false;
        public bool ConstraintVertical = false;


        private enum DragState
        {
            NONE,
            INIT,
            BEGIN,
            DRAGGING,
            RELEASING,
            APPROACHING
        }

        private DraggableAgent currentAgent;
        private DragState state;

        private IDraggableCornerHandler listener;
        private Vector2[] gridPoints = new Vector2[0];

        public new void Awake()
        {
            var listenerCandidate = transform.parent.GetComponent<IDraggableCornerHandler>();
            if (listenerCandidate != null)
            {
                // pass.
            }
            else
            {
                Debug.LogError("this:" + gameObject + " 's DraggableCorner handler is not found in parent:" + transform.parent.gameObject + ". please set IDraggableCornerHandler to parent GameObject before addComponent or instantiate.");
                return;
            }

            // non nullなのを確認した上でセットする
            listener = listenerCandidate;

            // baseの初期化時に実行する関数としてこのクラスの初期化処理をセット
            base.SetSubclassReloadAndExcludedAction(
                () =>
                {
                    var whole = ExposureAllContents();

                    // containedUIComponentsへと自動的にagentを生やす
                    // TODO: agent勝手に付けるよりももっといい方法があると思うが、、まあはい。
                    foreach (var content in whole)
                    {
                        // すでにセット済み
                        if (content.gameObject.GetComponent<DraggableAgent>() != null)
                        {
                            continue;
                        }

                        var agent = content.gameObject.AddComponent<DraggableAgent>();
                        agent.parent = this;
                    }

                    // 親のOnInitializedを着火する
                    var gridPointSources = listener.OnDraggableCornerInitialized(
                        (fromIndex, toIndex) =>
                        {
                            // TODO: コレはもしかすると対象のcornerが返せれば、それでいいのかもしれないが、それなら参照で持ってれば呼べるよねになるので難しい。どちらも問題を解決しない。
                            // driverが発見できればそれを返す
                            if (TryGetDriver(fromIndex, toIndex, out var driver))
                            {
                                return driver;
                            }
                            return null;
                        }
                    );

                    if (gridPointSources != null)
                    {
                        // このオブジェクトの幅、高さから、gridPointを生成する。
                        var currentSize = currentRectTransform().sizeDelta;
                        this.gridPoints = gridPointSources.Select(g => new Vector2(g.x * currentSize.x, g.y * currentSize.y)).ToArray();
                        return;
                    }

                    // grid point is null.
                    Debug.LogError("should set at least 1 point for drag target grid, e,g, return new Vector2[] { new Vector2(0.5f, 0.5f) }; makes the grid for center of the Draggable Corner.");
                },
                excludedGameObjectsFromCorner =>
                {
                    // excludeされた要素が発見されたので、削除を行う。
                    foreach (var excludedGameObjectFromCorner in excludedGameObjectsFromCorner)
                    {
                        // excludeされた対象の中でDraggableAgentを含んでいるものが見つかったので除外する。
                        if (excludedGameObjectFromCorner.TryGetComponent<DraggableAgent>(out var target))
                        {
                            Destroy(target);
                        }
                    }
                }
            );

            base.Awake();
        }

        private class DragObject
        {
            public readonly float TopSize;
            public readonly float RightSize;
            public readonly float BottomSize;
            public readonly float LeftSize;

            public readonly RectTransform contentRectTrans;
            public readonly Vector2 initialAnchoredPosition;

            public DragObject(RectTransform parent, RectTransform contentRectTrans, PointerEventData eventData)
            {
                /*
                    uGUI conrer内でのcontentの位置を知る必要がある。
                    親の幅からのアンカーで原点位置が変わる、pivotは重心。なので、x,y位置とかは、
                    centerX = 親のwidth * anchor min.x で決まる。
                    centerY = 親のheight * anchor min.y で決まる。

                    これがコンテンツの中心x,yになるので、ここからそれぞれコンテンツのwidth、heightをpivotの偏りだけ移動させた値が必要になる。
                */

                var contentLeftTop = GetLeftTopPosInParent(parent, contentRectTrans);

                // 移動可能なサイズを収集する。
                this.TopSize = Mathf.Max(0, contentLeftTop.y);
                this.RightSize = Mathf.Max(0, parent.sizeDelta.x - (contentLeftTop.x + contentRectTrans.sizeDelta.x));
                this.BottomSize = Mathf.Max(0, parent.sizeDelta.y - (contentLeftTop.y + contentRectTrans.sizeDelta.y));
                this.LeftSize = Mathf.Max(0, contentLeftTop.x);

                this.contentRectTrans = contentRectTrans;
                this.initialAnchoredPosition = contentRectTrans.anchoredPosition;
            }
        }

        private DragObject dragObject;

        public void OnInitializePotentialDrag(DraggableAgent agent, PointerEventData eventData)
        {
            switch (state)
            {
                case DragState.NONE:
                    // pass.
                    break;
                // 最終アニメーション動作中なので入力を無視する
                case DragState.RELEASING:
                case DragState.APPROACHING:
                    return;
                case DragState.INIT:
                    // 含まれるボタンを押したりすると発生する。無視していいやつ。
                    break;
                default:
                    Debug.LogError("OnInitializePotentialDrag unhandled state:" + state);
                    SetToNone();
                    return;
            }

            if (currentAgent == null)
            {
                // pass.
            }
            else
            {
                SetToNone();
                return;
            }

            // 開始
            state = DragState.INIT;
            currentAgent = agent;
        }

        public void OnBeginDrag(DraggableAgent agent, PointerEventData eventData)
        {
            switch (state)
            {
                case DragState.INIT:
                    // pass.
                    break;
                case DragState.RELEASING:
                case DragState.APPROACHING:
                    // すでにend中なので無視する
                    return;
                case DragState.NONE:
                    return;
                default:
                    // イレギュラーなので解除
                    SetToNone();
                    return;
            }

            if (currentAgent == null)
            {
                SetToNone();
                return;
            }

            if (currentAgent != agent)
            {
                Debug.Log("OnBeginDrag currentが切り替わった。");
                SetToNone();
                return;
            }

            // 対象物のサイズとこのオブジェクトのサイズに合わせて移動可能な範囲を出し、deltaを制限する。
            var currentDraggableRect = agent.GetComponent<RectTransform>();
            dragObject = new DragObject(currentRectTransform(), currentDraggableRect, eventData);

            // TODO: 必要があればconstraintを足そう。
            LimitDragValue(eventData, dragObject);

            // 動かす
            Move(dragObject, eventData.position);

            // TODO: 必要であれば書き換える。
            // 保持しているcornerと関連するcornerに対して、willDisappearとwillAppearを通知する。
            // NotifyAppearance(eventData.delta);

            state = DragState.BEGIN;
        }

        public void OnDrag(DraggableAgent agent, PointerEventData eventData)
        {
            // 特定状態以外無視する
            switch (state)
            {
                case DragState.BEGIN:
                    state = DragState.DRAGGING;
                    // pass.
                    break;
                case DragState.DRAGGING:
                    // pass.
                    break;
                case DragState.NONE:
                    // 無視する
                    return;
                case DragState.RELEASING:
                case DragState.APPROACHING:
                    // すでにend中なので無視する
                    return;
                default:
                    Debug.LogError("unhandled state:" + state);
                    SetToNone();
                    return;
            }

            if (currentAgent == null)
            {
                SetToNone();
                return;
            }

            if (currentAgent != agent)
            {
                Debug.Log("OnDrag currentが切り替わった。");
                SetToNone();
                return;
            }

            // TODO: このへんにmouse使ってて画面外みたいなのが必要

            // dragの方向制約処理を行う
            var actualMove = LimitDragValue(eventData, dragObject);

            // 動かす
            Move(dragObject, actualMove);

            if (gridPoints.Any())
            {
                var candidatePointIndex = -1;

                // オブジェクトの中心位置を取得する
                var centerPos = GetLeftTopPosInParent(currentRectTransform(), dragObject.contentRectTrans) + (dragObject.contentRectTrans.sizeDelta / 2);
                var distance = float.PositiveInfinity;

                for (var i = 0; i < gridPoints.Length; i++)
                {
                    var gridPoint = gridPoints[i];
                    var current = Vector2.Distance(gridPoint, centerPos);
                    if (current < distance)
                    {
                        // 更新
                        candidatePointIndex = i;
                    }
                    else if (current == distance)
                    {
                        // 同じ距離のものが出た場合、先勝ちにする。
                        break;
                    }

                    distance = current;
                }

                if (candidatePointIndex != -1)
                {
                    // 接近しているものがある場合、listenerへと伝える。
                    listener.OnDragApproachingToGrid(candidatePointIndex, dragObject.contentRectTrans.gameObject);
                }
            }
        }

        public void OnEndDrag(DraggableAgent agent, PointerEventData eventData)
        {
            // 特定状態以外無視する
            switch (state)
            {
                case DragState.DRAGGING:
                    // pass.
                    break;
                case DragState.NONE:
                    // 無視する
                    return;
                case DragState.RELEASING:
                case DragState.APPROACHING:
                    // すでにend中なので無視する
                    return;
                default:
                    Debug.LogError("unhandled state:" + state);
                    SetToNone();
                    return;
            }

            if (currentAgent == null)
            {
                SetToNone();
                return;
            }

            if (currentAgent != agent)
            {
                Debug.Log("OnEndDrag currentが切り替わった。");
                SetToNone();
                return;
            }

            // 正常にdragの終了に到達した

            /*
                gridがある場合、最も近いgridに吸い寄せられる。
            */
            var candidatePointIndex = -1;
            var diff = Vector2.zero;// 差分にすることで最短距離分だけ移動すればいいことにしている。
            if (gridPoints.Any())
            {
                // オブジェクトの中心位置を取得する
                var centerPos = GetLeftTopPosInParent(currentRectTransform(), dragObject.contentRectTrans) + (dragObject.contentRectTrans.sizeDelta / 2);
                var distance = float.PositiveInfinity;

                for (var i = 0; i < gridPoints.Length; i++)
                {
                    var gridPoint = gridPoints[i];
                    var current = Vector2.Distance(gridPoint, centerPos);
                    if (current < distance)
                    {
                        // 更新
                        candidatePointIndex = i;
                        diff = gridPoint - centerPos;
                    }
                    else if (current == distance)
                    {
                        // 同じ距離のものが出た場合、先勝ちにする。
                        break;
                    }

                    distance = current;
                }
            }

            // 最も近いgridが発見された
            if (candidatePointIndex != -1)
            {
                // 接近中状態にする。
                state = DragState.APPROACHING;
            }
            else
            {
                // リリース中状態にする
                state = DragState.RELEASING;
            }

            var approachTargetPoint = dragObject.contentRectTrans.anchoredPosition + new Vector2(diff.x, -diff.y);

            // enumeratorを回してアニメーション中状態を処理する
            IEnumerator animationCor()
            {
                while (true)
                {
                    switch (state)
                    {
                        // 終了処理中
                        case DragState.RELEASING:
                            var cancelCor = CancellingCor(candidatePointIndex);
                            while (cancelCor.MoveNext())
                            {
                                yield return null;
                            }

                            SetToNone();
                            yield break;
                        case DragState.APPROACHING:
                            var cor = ApproachingCor(approachTargetPoint, candidatePointIndex);
                            while (cor.MoveNext())
                            {
                                yield return null;
                            }

                            SetToNone();
                            yield break;

                        default:
                            // 何もしない
                            break;
                    }

                    yield return null;
                }
            }

            // animation用のCoroutineを作ってUpdateで回す。
            // こうすることで、不意に画面遷移が発生してもこのGOがなければ事故が発生しないようにする。
            this.animationCor = animationCor();
        }

        private IEnumerator ApproachingCor(Vector2 approachTargetPoint, int candidatePointIndex)
        {
            var done = false;
            Action onDone = () =>
            {
                done = true;
            };

            var cancelled = false;
            Action onCancelled = () =>
            {
                cancelled = true;
            };

            listener.OnDragApproachAnimationRequired(candidatePointIndex, dragObject.contentRectTrans.gameObject, approachTargetPoint, onDone, onCancelled);

            while (!cancelled && !done)
            {
                yield return null;
            }

            if (done)
            {
                // 位置をfixさせる
                SetToTargetPosition(dragObject, approachTargetPoint);

                // 通知を行う
                listener.OnDragReachedOnGrid(candidatePointIndex, dragObject.contentRectTrans.gameObject);
            }
            else if (cancelled)
            {
                var cancelCor = CancellingCor(candidatePointIndex);
                while (cancelCor.MoveNext())
                {
                    yield return null;
                }
            }
        }

        private IEnumerator CancellingCor(int candidatePointIndex)
        {
            var done = false;
            Action onDone = () =>
            {
                done = true;
            };

            // キャンセルアニメーション開始
            listener.OnDragBackAnimationRequired(dragObject.contentRectTrans.gameObject, dragObject.initialAnchoredPosition, onDone);

            while (!done)
            {
                yield return null;
            }

            // 位置をfixさせる
            ResetToInitialPosition(dragObject);

            // 通知を行う
            listener.OnDragBacked(candidatePointIndex, dragObject.contentRectTrans.gameObject);
        }


        private bool TryGetDriver(int fromIndex, int toIndex, out GamenDriver driver)
        {
            driver = null;

            // 現在対象のgridに載っているfromを見つけてくる
            if (!gridPoints.Any())
            {
                return false;
            }

            var distance = float.PositiveInfinity;
            var gridPoint = gridPoints[fromIndex];
            RectTransform candidatePointContentRectTrans = null;
            var toPosition = gridPoints[toIndex];
            var diff = Vector2.zero;// オブジェクトの中心位置と目的のgridの位置の差を取り、UI座標上での移動に利用する。
            foreach (var contentRectTrans in ExposureAllContents())
            {
                // オブジェクトの中心位置を取得する
                var centerPos = GetLeftTopPosInParent(currentRectTransform(), contentRectTrans) + (contentRectTrans.sizeDelta / 2);

                var current = Vector2.Distance(gridPoint, centerPos);
                if (current < distance)
                {
                    // 一番近いrectTrans候補を更新
                    candidatePointContentRectTrans = contentRectTrans;

                    // 目的値に対しての移動のために、diffを取得する。
                    diff = toPosition - centerPos;
                }
                else if (current == distance)
                {
                    // 同じ距離のものが出た場合、先勝ちにする。
                    break;
                }

                distance = current;
            }

            if (candidatePointContentRectTrans != null)
            {
                driver = new GamenDriver(
                    candidatePointContentRectTrans,
                    diff,
                    drivenRectTrans =>
                    {
                        listener.OnDragReachedOnGrid(toIndex, candidatePointContentRectTrans.gameObject);
                    }
                );
                return true;
            }

            return false;
        }

        private IEnumerator animationCor;

        private void SetToNone()
        {
            state = DragState.NONE;
            dragObject = null;
            currentAgent = null;
        }

        private void Update()
        {
            // アニメーション要素があれば動かす
            if (animationCor != null)
            {
                var cont = animationCor.MoveNext();
                if (!cont)
                {
                    animationCor = null;
                }
            }
        }

        // 移動させる
        private void Move(DragObject dragObject, Vector2 diff)
        {
            if (ConstraintHorizontal)
            {
                diff.y = 0;
            }
            if (ConstraintVertical)
            {
                diff.x = 0;
            }
            dragObject.contentRectTrans.anchoredPosition = dragObject.initialAnchoredPosition + diff;
        }

        // 位置を元に戻す
        private void ResetToInitialPosition(DragObject dragObject)
        {
            dragObject.contentRectTrans.anchoredPosition = dragObject.initialAnchoredPosition;
        }

        // 目的地に移動させる
        private void SetToTargetPosition(DragObject dragObject, Vector2 targetPoint)
        {
            dragObject.contentRectTrans.anchoredPosition = targetPoint;
        }


        // 移動幅の累積を制御する
        private Vector2 LimitDragValue(PointerEventData eventData, DragObject dragObj)
        {
            var x = 0f;
            var y = 0f;

            var basePos = eventData.pressPosition;// 押したworld位置
            var currentPos = eventData.position;// 現在のタッチのworld位置

            // 差分
            var baseDiff = currentPos - basePos;

            if (baseDiff.x < 0)
            {
                x = -Mathf.Min(Mathf.Abs(baseDiff.x), dragObj.LeftSize);
            }
            else if (0 < baseDiff.x)
            {
                x = Mathf.Min(baseDiff.x, dragObj.RightSize);
            }

            if (baseDiff.y < 0)
            {
                y = -Mathf.Min(Mathf.Abs(baseDiff.y), dragObj.TopSize);
            }
            else if (0 < baseDiff.y)
            {
                y = Mathf.Min(baseDiff.y, dragObj.BottomSize);
            }

            var result = new Vector2(x, y);

            return result;
        }

        // parentに対してcontentの左上の点がどの位置にあるかをVector2として返す。
        public static Vector2 GetLeftTopPosInParent(RectTransform parent, RectTransform contentRectTrans)
        {
            var contentCenterX = parent.sizeDelta.x * contentRectTrans.anchorMin.x;
            var contentCenterY = parent.sizeDelta.y * contentRectTrans.anchorMin.y;

            var contentCenterLeftTopX = contentCenterX - contentRectTrans.pivot.x * contentRectTrans.sizeDelta.x;
            var contentCenterLeftTopY = contentCenterY - contentRectTrans.pivot.y * contentRectTrans.sizeDelta.y;

            var contentLeftTopX = contentCenterLeftTopX + contentRectTrans.anchoredPosition.x;
            var contentLeftTopY = contentCenterLeftTopY - contentRectTrans.anchoredPosition.y;

            return new Vector2(contentLeftTopX, contentLeftTopY);
        }
    }
}