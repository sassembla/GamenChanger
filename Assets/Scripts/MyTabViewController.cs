using System;
using GamenChangerCore;
using UnityEngine;
using UnityEngine.UI;

public class MyTabViewController : MonoBehaviour, IOneOfNCornerHandler
{
    // 表示するコーナーと表示内容を保持しておくコーナーを複数持つ
    public Corner corner;
    public Corner corner1st;
    public Corner corner2nd;
    public Corner corner3rd;
    public Action<GameObject> setToOneOfNAct;

    public void OnOneOfNCornerReloaded(GameObject one, GameObject[] all, Action<GameObject> setToOneOfNAct)
    {
        // UIの調整
        foreach (var a in all)
        {
            var button = a.GetComponent<Button>();
            if (a == one)
            {
                button.interactable = false;
                continue;
            }
            button.interactable = true;
        }

        // oneの好きなパラメータで内容を変更する
        // TODO: とはいえstringは最悪なので、何かしらこのへんも宣言的にしたいところ。indexとか？ oneOfNみたいな概念でラップすればつけられるな。ただ結局どう並ぶかの法則を知らないと困る。
        switch (one.name)
        {
            case "Button1":
                corner.BackContentsIfNeed();
                corner.TryBorrowContents(corner1st);
                break;
            case "Button2":
                corner.BackContentsIfNeed();
                corner.TryBorrowContents(corner2nd);
                break;
            case "Button3":
                corner.BackContentsIfNeed();
                corner.TryBorrowContents(corner3rd);
                break;
            default:
                Debug.LogError("unhandled name:" + one.gameObject.name);
                break;
        }

        this.setToOneOfNAct = setToOneOfNAct;
    }

    public bool OneOfNCornerShouldAcceptInput()
    {
        return true;
    }

    public void OnOneOfNChangedToOneByPlayer(GameObject one, GameObject before, GameObject[] all)
    {
        // UIの調整
        foreach (var a in all)
        {
            var button = a.GetComponent<Button>();
            if (a == one)
            {
                button.interactable = false;
                continue;
            }
            button.interactable = true;
        }

        // oneの好きなパラメータで内容を変更する
        // TODO: とはいえstringは最悪なので、何かしらこのへんも宣言的にしたいところ。indexとか？ oneOfNみたいな概念でラップすればつけられるな。ただ結局どう並ぶかの法則を知らないと困る。
        switch (one.name)
        {
            case "Button1":
                corner.BackContentsIfNeed();
                corner.TryBorrowContents(corner1st);
                break;
            case "Button2":
                corner.BackContentsIfNeed();
                corner.TryBorrowContents(corner2nd);
                break;
            case "Button3":
                corner.BackContentsIfNeed();
                corner.TryBorrowContents(corner3rd);
                break;
            default:
                Debug.LogError("unhandled name:" + one.gameObject.name);
                break;
        }
    }

    public void OnOneOfNChangedToOneByHandler(GameObject one, GameObject before, GameObject[] all)
    {
        // UIの調整
        foreach (var a in all)
        {
            var button = a.GetComponent<Button>();
            if (a == one)
            {
                button.interactable = false;
                continue;
            }
            button.interactable = true;
        }
    }
}