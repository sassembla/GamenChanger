using UnityEngine;
using GamenChangerCore;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

public class MyFlickableSegueViewController : MonoBehaviour, IOneOfNCornerHandler
{
    public MyFlickDetector flickDetector;

    public void OnInitialized(GameObject one, GameObject[] all)
    {
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

    public void OnChangedToOne(GameObject one, GameObject before, GameObject[] all)
    {
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

            var (isFound, animation) = FlickableCorner.TryFindingAutoFlickRoute(fromFlickableCorner, targetFlickableCorner);
            if (isFound)
            {
                // 開始する
                StartCoroutine(animation);
            }
        }
    }

    // oneOfNのoneがどこかでコードから選択された際に呼ばれる
    public GameObject OnSelectOneOfNFromCodeWithCorner(Corner corner, GameObject[] all)
    {
        switch (corner.name)
        {
            case "FrickableCorner1":
                return all[0];
            case "FrickableCorner2":
                return all[1];
            case "FrickableCorner3":
                return all[2];
            case "FrickableCorner4":
                return all[3];
            default:
                Debug.LogError("unhandled corner:" + corner.name);
                break;
        }

        return null;
    }


}
