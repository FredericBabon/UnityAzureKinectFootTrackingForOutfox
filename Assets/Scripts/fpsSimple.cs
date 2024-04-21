using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;
using System.Collections.Concurrent;



public class fpsSimple : MonoBehaviour {
	[SerializeField] TextMeshProUGUI fpsText;
	public float deltaTime;

	float computed_fps=30.0f;    

    //FixedSizedQueue fps_queue = new FixedSizedQueue(30);

    void Start()
    {
        //InvokeRepeating("CheckFpsRegularly", 1.0f, 2.5f);
        //this.gameObject.GetComponent<CanvasRenderer>().SetAlpha(0.0F);
    }

    /*void CheckFpsRegularly()
    {
        float mean_fps = fps_queue.GetMean();
        //Debug.Log("CheckFpsRegularly:"+ mean_fps);       
        
    }*/

    void Update () {
		deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
        computed_fps = 1.0f / deltaTime;
        //fps_queue.Enqueue(computed_fps);

        fpsText.text = Mathf.Ceil (computed_fps).ToString ();
	}
}