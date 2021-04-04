using UnityEngine;
using GamenChangerCore;
using UnityEngine.UI;
using System.Collections;
using System;

public class MyFlickableSegueViewController : MonoBehaviour, IOneOfNCornerHandler
{
    public MyFlickDetector flickDetector;

    public void OnOneOfNCornerReloaded(GameObject one, GameObject[] all, Action<GameObject> setToOneOfNAct)
    {
        // segueが初期化された

        // UIの調整
        foreach (var a in all)
        {
            var button = a.GetComponent<Button>();

            // aがoneと一緒だった場合、選択されているのでinteractableをfalseにする。
            if (a == one)
            {
                button.interactable = false;
                continue;
            }

            // 選択されていないのでinteractableをtrueにする。
            button.interactable = true;
        }

        // フリックの結果をOneOfNの選択に紐づける
        flickDetector.selectIndicator = index => setToOneOfNAct(all[index]);
    }

    public bool OneOfNCornerShouldAcceptInput()
    {
        return true;
    }

    public void OnOneOfNChangedToOneByPlayer(GameObject one, GameObject before, GameObject[] all)
    {
        // sequeのどれか一つがUI操作によって更新された

        // UIの調整
        foreach (var a in all)
        {
            var button = a.GetComponent<Button>();

            // aがoneと一緒だった場合、選択されているのでinteractableをfalseにする。
            if (a == one)
            {
                button.interactable = false;
                continue;
            }

            // 選択されていないのでinteractableをtrueにする。
            button.interactable = true;
        }

        // flickDetectorからCornerを取得し、そこに含まれているFlickableCorner群を取り出し、どれか目指して進む。
        var corner = flickDetector.GetComponent<Corner>();
        if (corner.TryExposureCorners<FlickableCorner>(out var flickableCorners))
        {
            // fromの検出
            FlickableCorner fromFlickableCorner = null;
            switch (before.name)
            {
                case "Button1":
                    fromFlickableCorner = flickableCorners[0];
                    break;
                case "Button2":
                    fromFlickableCorner = flickableCorners[1];
                    break;
                case "Button3":
                    fromFlickableCorner = flickableCorners[2];
                    break;
                case "Button4":
                    fromFlickableCorner = flickableCorners[3];
                    break;
                default:
                    Debug.LogError("unhandled:" + one.name);
                    return;
            }

            // toの検出
            FlickableCorner targetFlickableCorner = null;
            switch (one.name)
            {
                case "Button1":
                    targetFlickableCorner = flickableCorners[0];
                    break;
                case "Button2":
                    targetFlickableCorner = flickableCorners[1];
                    break;
                case "Button3":
                    targetFlickableCorner = flickableCorners[2];
                    break;
                case "Button4":
                    targetFlickableCorner = flickableCorners[3];
                    break;
                default:
                    Debug.LogError("unhandled:" + one.name);
                    return;
            }

            // sequeが操作されたので、flickableCornerの中でフォーカスしてあるものを変更する。
            if (FlickableCorner.TryFindingAutoFlickRoute(fromFlickableCorner, targetFlickableCorner, out var driver))
            {
                // TODO: このへんでdriverを持っておくとおもしろそう。stopしたいので、、
                IEnumerator driveCor()
                {
                    while (driver.MoveNext())
                    {
                        yield return null;
                    }
                };

                // 開始する
                StartCoroutine(driveCor());
            }
        }
    }

    public void OnOneOfNChangedToOneByHandler(GameObject one, GameObject before, GameObject[] all)
    {
        // sequeのどれか一つがhandlerによって更新された

        // UIの調整
        foreach (var a in all)
        {
            var button = a.GetComponent<Button>();

            // aがoneと一緒だった場合、選択されているのでinteractableをfalseにする。
            if (a == one)
            {
                button.interactable = false;
                continue;
            }

            // 選択されていないのでinteractableをtrueにする。
            button.interactable = true;
        }
    }


}
