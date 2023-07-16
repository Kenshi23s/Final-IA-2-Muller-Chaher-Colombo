using AYellowpaper.SerializedCollections;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Flag_UI : MonoBehaviour
{
    [SerializeField] Gradient _gradientFlag;
    [SerializeField] Image _flagOwner;
    [SerializeField,SerializedDictionary("Team","TextMesh")] SerializedDictionary<MilitaryTeam, TextMeshProUGUI> TeamTexts;

    CapturePoint _myCapturePoint;
    private void Awake()
    {
         _myCapturePoint = GetComponentInParent<CapturePoint>();

        if (_myCapturePoint == null) Destroy(gameObject);

        _myCapturePoint.onProgressChange.AddListener(SetImageValue);

        _myCapturePoint.onEntitiesAroundUpdate.AddListener(SetTexts);
        SetImageValue();
    }

    private void LateUpdate()
    {
        transform.forward = (Camera.main.transform.position - transform.position).normalized;
    }

    void SetImageValue()
    {
        _flagOwner.color = _gradientFlag.Evaluate(_myCapturePoint.ZoneProgressNormalized);
    }


    void SetTexts(Dictionary<MilitaryTeam, IMilitary[]> col)
    {
        foreach (var key in TeamTexts.Keys.Where(x => col[x] != null ))
        {
            TeamTexts[key].text = col[key].Length.ToString();
        }
    }
  
    private void OnValidate()
    {
      
    }
}
