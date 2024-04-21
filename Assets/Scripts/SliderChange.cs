using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SliderChange : MonoBehaviour
{
    public bool setInvisibleAtStartup = true;
    public Slider slider;
    public TextMeshProUGUI sliderText;


    public GameObject arrow;
    // Start is called before the first frame update
    void Start()
    {
        if(PlayerPrefs.HasKey(slider.name))
        {
            slider.value = PlayerPrefs.GetFloat(slider.name);
        }
        SliderChanged();
        slider.onValueChanged.AddListener (delegate {SliderChanged ();});
        if (setInvisibleAtStartup) 
            slider.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void SliderChanged()
    {
        PlayerPrefs.SetFloat(slider.name, slider.value);

        sliderText.text = slider.value.ToString();
        Vector3 centerVec3 = arrow.GetComponent<BoxCollider>().center;
        centerVec3.y = slider.value;
        arrow.GetComponent<BoxCollider>().center = centerVec3;
    }


    public void Reset()
    {
        slider.value = -0.25f;
        SliderChanged();
    }
}