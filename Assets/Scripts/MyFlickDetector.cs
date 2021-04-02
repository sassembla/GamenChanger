using System;
using System.Collections;
using GamenChangerCore;
using UnityEngine;
using UnityEngine.UI;

public class MyFlickDetector : MonoBehaviour, IFlickableCornerHandler
{
    public Action<int> selectIndicator;

    public GameObject FlickableCornerPrefab;

    /*
        flickableCornerに対して、flick操作に応じて関連するcornerの出現や消滅、progressを取得することができる。
        また、対象方向へのflick先が存在しない場合、OnFlickRequestFromFlickableCornerが呼び出され、その中でcornerを追加したり接続を切り替えることで
        無限にflick先を生成する、などができる。
    */
    public void Touch(FlickableCorner flickableCorner)
    {
        Debug.Log("Touch:" + flickableCorner);
        // TODO: この辺をどうにかして上位に伝えると、driveのキャンセルができるようになる。
    }

    public void WillAppear(FlickableCorner flickableCorner)
    {
        Debug.Log("WillAppear:" + flickableCorner);
    }
    public void DidAppear(FlickableCorner flickableCorner)
    {
        Debug.Log("DidAppear:" + flickableCorner);
        switch (flickableCorner.name)
        {
            case "FrickableCorner1":
                selectIndicator(0);
                break;
            case "FrickableCorner2":
                selectIndicator(1);
                break;
            case "FrickableCorner3":
                selectIndicator(2);
                break;
            case "FrickableCorner4":
                selectIndicator(3);
                break;
            default:
                Debug.Log("unhandled corner:" + flickableCorner.name);
                break;
        }
    }

    public void AppearCancelled(FlickableCorner flickableCorner)
    {
        // Debug.Log("AppearCancelled:" + flickableCorner);
        if (flickableCorner.gameObject.name.Contains("FrickableCornerPrefab"))
        {
            // prefabから作ったやつだったら消す
            Destroy(flickableCorner.gameObject);
        }
    }
    public void AppearProgress(FlickableCorner flickableCorner, float progress)
    {
        // Debug.Log("AppearProgress:" + flickableCorner + " progress:" + progress);
    }


    public void WillDisappear(FlickableCorner flickableCorner)
    {
        // Debug.Log("WillDisappear:" + flickableCorner);
    }
    public void DidDisappear(FlickableCorner flickableCorner)
    {
        // Debug.Log("DidDisappear:" + flickableCorner);
        if (flickableCorner.gameObject.name.Contains("FrickableCornerPrefab"))
        {
            // prefabから作ったやつだったら消す
            Destroy(flickableCorner.gameObject);
        }
    }
    public void DisppearCancelled(FlickableCorner flickableCorner)
    {
        // Debug.Log("DisppearCancelled:" + flickableCorner);
    }
    public void DisppearProgress(FlickableCorner flickableCorner, float progress)
    {
        // Debug.Log("DisppearProgress:" + flickableCorner + " progress:" + progress);
    }


    private int count = 0;
    // フリックの開始時にリクエストを検知し、ビューの建て増しと削除が可能になる。
    public void OnFlickRequestFromFlickableCorner(FlickableCorner flickingCorner, ref Corner cornerFromLeft, ref Corner cornerFromRight, ref Corner cornerFromTop, ref Corner cornerFromBottom, FlickDirection plannedFlickDir)
    {
        Debug.Log("OnFlickRequestFromFlickableCorner:" + Time.frameCount);
        // leftが空なFlickableCornerに対して右フリックをした際、左側にコンテンツを偽造する
        if (plannedFlickDir == FlickDirection.RIGHT && cornerFromLeft == null)
        {
            // cornerFromLeftに代入する
            var newCorner = Instantiate(FlickableCornerPrefab, this.transform).GetComponent<FlickableCorner>();

            // 作成したcornerを、現在flick操作中のcornerの左側にくるようにセット
            newCorner.currentRectTransform().anchoredPosition = new Vector2(flickingCorner.currentRectTransform().anchoredPosition.x - flickingCorner.currentRectTransform().sizeDelta.x, flickingCorner.currentRectTransform().anchoredPosition.y);
            newCorner.CornerFromRight = flickingCorner;
            cornerFromLeft = newCorner;
            count++;

            // ボタンにカウント表示をセットする
            if (newCorner.TryExposureContents<Button>(out var buttons))
            {
                var text = buttons[0].GetComponentInChildren<Text>();
                text.text = text.text + ":" + count;
            }
            else
            {
                Debug.LogError("ボタンがない 1");
            }
            return;
        }

        // rightが空なFlickableCornerに対して左フリックをした際、右側にコンテンツを偽造する
        if (plannedFlickDir == FlickDirection.LEFT && cornerFromRight == null)
        {
            // cornerFromRightに代入する
            var newCorner = Instantiate(FlickableCornerPrefab, this.transform).GetComponent<FlickableCorner>();

            // 作成したcornerを、現在flick操作中のcornerの右側にくるようにセット
            newCorner.currentRectTransform().anchoredPosition = new Vector2(flickingCorner.currentRectTransform().anchoredPosition.x + flickingCorner.currentRectTransform().sizeDelta.x, flickingCorner.currentRectTransform().anchoredPosition.y);
            newCorner.CornerFromLeft = flickingCorner;
            cornerFromRight = newCorner;
            count++;

            // ボタンにカウント表示をセットする
            if (newCorner.TryExposureContents<Button>(out var buttons))
            {
                var text = buttons[0].GetComponentInChildren<Text>();
                text.text = text.text + ":" + count;
            }
            else
            {
                Debug.LogError("ボタンがない 2");
            }
            return;
        }
    }

    public void OnProcessAnimationRequired(FlickableCorner flickableCorner, Vector2 targetPosition, Action onDone, Action onCancelled)
    {
        var rectTrans = flickableCorner.GetComponent<RectTransform>();
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
                    yield break;
                }
                count++;
                yield return null;
            }
        }
        StartCoroutine(process());
    }

    public void OnCancelAnimationRequired(FlickableCorner flickableCorner, Vector2 initialPosition, Action onDone)
    {
        var rectTrans = flickableCorner.GetComponent<RectTransform>();
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
                    yield break;
                }
                count++;
                yield return null;
            }
        }
        StartCoroutine(cancel());
    }
}
