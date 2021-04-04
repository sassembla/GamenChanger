using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GamenChangerCore;
using UnityEngine;
using UnityEngine.UI;

// ドラッグ可能なSegueを持ち、それと連動するフリックが可能なビューがある画面
public class MyDraggableSegueViewController : MonoBehaviour, IOneOfNCornerHandler, IDraggableCornerHandler, IFlickableCornerHandler, ICanvasRaycastFilter
{
    public Button FloatButton;
    public DraggableCorner SegDraggableFloatButtonCorner;
    public OneOfNCorner SegOneOfNButtonsCorner;
    public Corner FlickableCornersCorner;

    private Action set1Of4Act;
    private Action set2Of4Act;
    private Action set3Of4Act;
    private Action set4Of4Act;
    private Func<int, int, GamenDriver> draggableDriver;

    void Start()
    {
        // iOS/Android用に60FPS化
        Application.targetFrameRate = 60;

        // DraggableはfloatButtonのみを含み、OneOfはfloatButton以外を含むようにする。
        // DraggableとOneOfを相互に排他の状態にする。

        // floatButtonとFlickableCornerをOneOfNから取り除く
        SegOneOfNButtonsCorner.ExcludeContent(FloatButton);
        SegOneOfNButtonsCorner.ExcludeCorners<FlickableCorner>();

        // floatButton以外をDraggableから取り除く
        if (SegOneOfNButtonsCorner.TryExposureContents<Button>(out var buttons))
        {
            var buttonList = buttons.ToList();
            buttonList.Remove(FloatButton);
            SegDraggableFloatButtonCorner.ExcludeContents(buttonList.ToArray());
        }
    }

    // プレイヤーの行動 = UI操作の結果、Sequeを選択する。
    private void SelectSegue(int index)
    {
        switch (index)
        {
            case 0:
                set1Of4Act();
                break;
            case 1:
                set2Of4Act();
                break;
            case 2:
                set3Of4Act();
                break;
            case 3:
                set4Of4Act();
                break;
            default:
                Debug.LogError("unhandled index:" + index);
                break;
        }
    }

    public Vector2[] OnDraggableCornerInitialized(Func<int, int, GamenDriver> draggableDriver)
    {
        // このdraggable用のdriverを取得しておく
        this.draggableDriver = draggableDriver;

        // floatButtonが乗るグリッドを返す
        return new Vector2[] {
            new Vector2(0.25f / 2f, 0.5f),
            new Vector2(0.25f + 0.25f / 2f, 0.5f),
            new Vector2(0.25f*2 + 0.25f / 2f, 0.5f),
            new Vector2(0.25f*3 + 0.25f / 2f, 0.5f),
        };
    }


    public void OnDragApproachAnimationRequired(int index, GameObject go, Vector2 approachTargetPosition, Action onDone, Action onCancelled)
    {
        var rectTrans = go.GetComponent<RectTransform>();
        animating = true;
        var waitGroup = new WaitGroup(
            () => animating = false
        );
        waitGroup.AddWait(rectTrans);
        IEnumerator approach()
        {
            var count = 0;
            while (true)
            {
                rectTrans.anchoredPosition = rectTrans.anchoredPosition + (approachTargetPosition - rectTrans.anchoredPosition) * 0.5f;
                if (count == 10)
                {
                    onDone();
                    waitGroup.RemoveWait(rectTrans);
                    yield break;
                }
                count++;
                yield return null;
            }
        }
        StartCoroutine(approach());
    }

    public void OnDragBackAnimationRequired(GameObject go, Vector2 initialPosition, Action onDone)
    {
        var rectTrans = go.GetComponent<RectTransform>();
        animating = true;
        var waitGroup = new WaitGroup(
            () => animating = false
        );
        waitGroup.AddWait(rectTrans);
        IEnumerator cancel()
        {
            var count = 0;
            while (true)
            {
                rectTrans.anchoredPosition = rectTrans.anchoredPosition + (initialPosition - rectTrans.anchoredPosition) * 0.5f;
                if (count == 10)
                {
                    onDone();
                    waitGroup.RemoveWait(rectTrans);
                    yield break;
                }
                count++;
                yield return null;
            }
        }
        StartCoroutine(cancel());
    }

    public void OnDragBacked(int index, GameObject go)
    {
        // 何もしない
    }

    public void OnDragApproachingToGrid(int index, GameObject go)
    {
        // 表示を更新する
        if (go.TryGetComponent<Button>(out var button))
        {
            var buttonText = button.GetComponentInChildren<Text>();
            buttonText.text = "Button" + (index + 1);
        }
    }

    public void OnDragReachedOnGrid(int index, GameObject go)
    {
        // 表示を更新する
        OnDragApproachingToGrid(index, go);

        var fromIndex = SegOneOfNButtonsCorner.GetIndexOfOne();

        // 変化がないので終了
        if (index == fromIndex)
        {
            return;
        }

        // dragが終わったgridのindexを元に、sequeを選択した状態にする。
        SelectSegue(index);

        // flickableを移動する
        if (FlickableCornersCorner.TryExposureCorners<FlickableCorner>(out var flickableCorners))
        {
            // from to の導出
            var fromFlickableCorner = flickableCorners[fromIndex];
            var targetFlickableCorner = flickableCorners[index];

            // sequeが操作されたので、flickableCornerの中でフォーカスしてあるものを変更する。
            if (FlickableCorner.TryFindingAutoFlickRoute(fromFlickableCorner, targetFlickableCorner, out var flickDriver))
            {
                // TODO: このへんでdriverを持っておくとおもしろそう。stopしたいので、、

                animating = true;

                var wait = new WaitGroup(
                    () => animating = false
                );

                wait.AddWait(flickDriver);
                IEnumerator driveCor()
                {
                    while (flickDriver.MoveNext())
                    {
                        yield return null;
                    }
                    wait.RemoveWait(flickDriver);
                };

                // 開始する
                StartCoroutine(driveCor());
            }
        }
    }


    // oneOfNシリーズ

    public void OnOneOfNCornerReloaded(GameObject one, GameObject[] all, Action<GameObject> setOneOfNAct)
    {
        this.set1Of4Act = () => setOneOfNAct(all[0]);
        this.set2Of4Act = () => setOneOfNAct(all[1]);
        this.set3Of4Act = () => setOneOfNAct(all[2]);
        this.set4Of4Act = () => setOneOfNAct(all[3]);
    }
    public bool OneOfNCornerShouldAcceptInput()
    {
        // アニメーション中でなければイベントを継続する
        return !animating;
    }

    public void OnOneOfNChangedToOneByHandler(GameObject one, GameObject before, GameObject[] all)
    {
        // OneOfがコードから選択された。特に何もしない。
    }

    public void OnOneOfNChangedToOneByPlayer(GameObject one, GameObject before, GameObject[] all)
    {
        // Debug.Log("OnOneOfNChangedToOneByPlayer animating:" + animating + " frame:" + Time.frameCount);

        // プレイヤーがUI上のButton1~4のどれかを押したので、flickViewとfloatButtonを移動させる。
        if (FlickableCornersCorner.TryExposureCorners<FlickableCorner>(out var flickableCorners))
        {
            // fromの検出
            var fromIndex = -1;
            switch (before.name)
            {
                case "Button1":
                    fromIndex = 0;
                    break;
                case "Button2":
                    fromIndex = 1;
                    break;
                case "Button3":
                    fromIndex = 2;
                    break;
                case "Button4":
                    fromIndex = 3;
                    break;
                default:
                    Debug.LogError("unhandled:" + one.name);
                    return;
            }

            // toの検出
            var toIndex = -1;
            switch (one.name)
            {
                case "Button1":
                    toIndex = 0;
                    break;
                case "Button2":
                    toIndex = 1;
                    break;
                case "Button3":
                    toIndex = 2;
                    break;
                case "Button4":
                    toIndex = 3;
                    break;
                default:
                    Debug.LogError("unhandled:" + one.name);
                    return;
            }

            // from to の導出
            var fromFlickableCorner = flickableCorners[fromIndex];
            var targetFlickableCorner = flickableCorners[toIndex];

            // sequeが操作されたので、flickableCornerの中でフォーカスしてあるものを変更する。
            if (FlickableCorner.TryFindingAutoFlickRoute(fromFlickableCorner, targetFlickableCorner, out var flickDriver))
            {
                // TODO: このへんでdriverを持っておくとおもしろそう。stopしたいので、、

                // draggableをtoIndexに向けて動かす
                var dragDriver = draggableDriver(fromIndex, toIndex);

                animating = true;

                var wait = new WaitGroup(
                    () => animating = false
                );

                wait.AddWait(flickDriver);
                wait.AddWait(dragDriver);

                IEnumerator flickDriveCor()
                {
                    while (flickDriver.MoveNext())
                    {
                        yield return null;
                    }
                    wait.RemoveWait(flickDriver);
                };
                IEnumerator dragDriveCor()
                {
                    while (dragDriver.MoveNext())
                    {
                        yield return null;
                    }
                    wait.RemoveWait(dragDriver);
                };

                // 開始する
                StartCoroutine(flickDriveCor());
                StartCoroutine(dragDriveCor());
            }
        }
    }

    // flicableCorner関連

    public bool OnFlickRequestFromFlickableCorner(FlickableCorner flickableCorner, ref Corner cornerFromLeft, ref Corner cornerFromRight, ref Corner cornerFromTop, ref Corner cornerFromBottom, FlickDirection plannedFlickDir)
    {
        return true;
    }

    public void FlickableCornerWillBack(FlickableCorner flickableCorner)
    {
        // 何もしない
    }

    public void FlickableCornerWillCancel(FlickableCorner flickableCorner)
    {
        // 何もしない
    }

    public void FlickableCornerWillAppear(FlickableCorner flickableCorner)
    {
        // Debug.Log("FlickableCornerWillAppear animating:" + animating);
        var targetIndex = -1;
        switch (flickableCorner.name)
        {
            case "FlickableCorner1":
                targetIndex = 0;
                break;
            case "FlickableCorner2":
                targetIndex = 1;
                break;
            case "FlickableCorner3":
                targetIndex = 2;
                break;
            case "FlickableCorner4":
                targetIndex = 3;
                break;
            default:
                Debug.LogError("unhandled corner:" + flickableCorner);
                break;
        }

        // 該当なし
        if (targetIndex == -1)
        {
            return;
        }

        var currentIndexOfSegue = SegOneOfNButtonsCorner.GetIndexOfOne();

        // 現在既に選択されているindexだった場合、何も起こらない
        if (currentIndexOfSegue == targetIndex)
        {
            return;
        }

        // floatButtonを移動させる
        {
            var dragDriver = draggableDriver(currentIndexOfSegue, targetIndex);

            animating = true;

            var wait = new WaitGroup(
                () => animating = false
            );

            wait.AddWait(dragDriver);
            IEnumerator driveCor()
            {
                while (dragDriver.MoveNext())
                {
                    yield return null;
                }

                wait.RemoveWait(dragDriver);
            };

            // 開始する
            StartCoroutine(driveCor());
        }

        // 特定のindexのsequeを選択状態にする。
        SelectSegue(targetIndex);
    }

    public void FlickableCornerAppearProgress(FlickableCorner flickableCorner, float progress)
    {
        // 何もしない
    }

    public void FlickableCornerAppearCancelled(FlickableCorner flickableCorner)
    {
        // 何もしない
    }

    public void FlickableCornerDidAppear(FlickableCorner flickableCorner)
    {
        // 何もしない
    }

    public void FlickableCornerWillDisappear(FlickableCorner flickableCorner)
    {
        // 何もしない
    }

    public void FlickableCornerDisppearProgress(FlickableCorner flickableCorner, float progress)
    {
        // 何もしない
    }

    public void FlickableCornerDisppearCancelled(FlickableCorner flickableCorner)
    {
        // 何もしない
    }

    public void FlickableCornerDidDisappear(FlickableCorner flickableCorner)
    {
        // 何もしない
    }

    public void OnFlickProcessAnimationRequired(FlickableCorner flickableCorner, Vector2 targetPosition, Action onDone, Action onCancelled)
    {
        var rectTrans = flickableCorner.GetComponent<RectTransform>();
        animating = true;
        var waitGroup = new WaitGroup(
            () => animating = false
        );
        waitGroup.AddWait(rectTrans);
        IEnumerator process()
        {
            var count = 0;
            while (true)
            {
                rectTrans.anchoredPosition = rectTrans.anchoredPosition + (targetPosition - rectTrans.anchoredPosition) * 0.5f;
                flickableCorner.UpdateRelatedCornerPositions();

                if (count == 10)
                {
                    onDone();
                    waitGroup.RemoveWait(rectTrans);
                    yield break;
                }
                count++;
                yield return null;
            }
        }
        StartCoroutine(process());
    }

    public void OnFlickCancelAnimationRequired(FlickableCorner flickableCorner, Vector2 initialPosition, Action onDone)
    {
        var rectTrans = flickableCorner.GetComponent<RectTransform>();
        animating = true;
        var waitGroup = new WaitGroup(
           () => animating = false
       );
        waitGroup.AddWait(rectTrans);
        IEnumerator cancel()
        {
            var count = 0;
            while (true)
            {
                rectTrans.anchoredPosition = rectTrans.anchoredPosition + (initialPosition - rectTrans.anchoredPosition) * 0.5f;
                flickableCorner.UpdateRelatedCornerPositions();

                if (count == 10)
                {
                    onDone();
                    waitGroup.RemoveWait(rectTrans);
                    yield break;
                }
                count++;
                yield return null;
            }
        }
        StartCoroutine(cancel());
    }

    // 適当な実装のWaitGroup
    private class WaitGroup
    {
        private readonly Action onWaitOver;
        private readonly List<object> waitList;

        public WaitGroup(Action onWaitOver)
        {
            this.onWaitOver = onWaitOver;
            this.waitList = new List<object>();
        }

        public void AddWait(object o)
        {
            if (waitList.Contains(o))
            {
                return;
            }

            waitList.Add(o);
        }

        public void RemoveWait(object o)
        {
            if (!waitList.Contains(o))
            {
                Debug.LogError("remove failed.");
                return;
            }

            waitList.Remove(o);

            // まだ残りがある
            if (waitList.Any())
            {
                return;
            }

            onWaitOver();
        }
    }

    // アニメーション中のUI操作を受け付けない(体感がよくないのでおすすめはしないがここではこうするとUI操作を遮断できるよという話までに)
    private bool animating;
    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        // Debug.Log("animating:" + animating + " frame:" + Time.frameCount);// TODO: あー同じ条件でdragも無効化すればいいのか。
        return !animating;
    }
}