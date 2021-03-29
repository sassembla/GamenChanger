using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamenChangerCore
{
    public class DraggableCorner : Corner
    {
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
        private Vector2[] gridPoints;

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
            base.SetSubclassReloadAction(
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
                    var gridPointSources = listener.OnInitialized();

                    // このオブジェクトの幅、高さから、gridPointを生成する。
                    var cirrentSize = currentRectTransform().sizeDelta;
                    this.gridPoints = gridPointSources.Select(g => new Vector2(g.x * cirrentSize.x, g.y * cirrentSize.y)).ToArray();
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

            // dragの方向制約処理を行う
            var actualMove = LimitDragValue(eventData, dragObject);

            // 動かす
            Move(dragObject, actualMove);

            if (gridPoints != null && gridPoints.Any())
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
            if (gridPoints != null && gridPoints.Any())
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
                            // TODO: 達成するところのアニメーションはなんか自由に頑張ってくれってやりたいんだよな、どうするかな。

                            ResetToInitialPosition(dragObject);

                            SetToNone();
                            yield break;
                        case DragState.APPROACHING:
                            // TODO: 達成するところのアニメーションはなんか自由に頑張ってくれってやりたいんだよな、どうするかな。

                            // 位置をfixさせる
                            SetToTargetPosition(dragObject, approachTargetPoint);

                            // 通知を行う
                            listener.OnGrid(candidatePointIndex, dragObject.contentRectTrans.gameObject);

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