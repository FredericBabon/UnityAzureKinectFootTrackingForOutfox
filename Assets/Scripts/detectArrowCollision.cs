using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class detectArrowCollision : MonoBehaviour
{
    public int nbcollisions = 0;
    public GameObject lumiere;



    // Start is called before the first frame update
    void Start()
    {
        
    }

    void OnCollisionEnter(Collision infoCollision) // le type de la variable est Collision
    {
        if (infoCollision.gameObject.name.IndexOf("arrow") == -1)
        {
            if (nbcollisions == 0)
            {
                Debug.Log("OnCollisionEnter:" + gameObject.name + "-" + infoCollision.gameObject.name);
                lumiere.SetActive(true);

                if (gameObject.name.IndexOf("leftarrow") == 0)
                    Keyboard.KeyDown(System.Windows.Forms.Keys.Left);//KEYCODE.VK_LEFT);
                else if (gameObject.name.IndexOf("rightarrow") == 0)
                    Keyboard.KeyDown(System.Windows.Forms.Keys.Right);//KEYCODE.VK_RIGHT);
                else if (gameObject.name.IndexOf("uparrow") == 0)
                    Keyboard.KeyDown(System.Windows.Forms.Keys.Up);//KEYCODE.VK_UP);
                else if (gameObject.name.IndexOf("backarrow") == 0)
                    Keyboard.KeyDown(System.Windows.Forms.Keys.Down);//KEYCODE.VK_DOWN);
            }
            nbcollisions++;
        }        
    }

    void OnCollisionStay(Collision infoCollision)
    {
        //Debug.Log("OnCollisionStay:" + infoCollision.gameObject.name);
        // Debug-draw all contact points and normals
        /*foreach (ContactPoint contact in infoCollision.contacts)
        {
            Debug.DrawRay(contact.point, contact.normal, Color.white);
        }*/



    }
    void OnCollisionExit(Collision infoCollision)
    {
        //if(infoCollision.gameObject.name!="Plane")

        if (infoCollision.gameObject.name.IndexOf("arrow") == -1)
        {
            nbcollisions--;
            print("No longer in contact with " + gameObject.name + "-" + infoCollision.gameObject.name);
            if (nbcollisions == 0)
            {
                triggerOffArrow();
            }
        }
        
    }

    void triggerOffArrow()
    {
        lumiere.SetActive(false);

        if (gameObject.name.IndexOf("leftarrow") == 0)
            Keyboard.KeyUp(System.Windows.Forms.Keys.Left);//KEYCODE.VK_LEFT);
        else if (gameObject.name.IndexOf("rightarrow") == 0)
            Keyboard.KeyUp(System.Windows.Forms.Keys.Right);//KEYCODE.VK_RIGHT);
        else if (gameObject.name.IndexOf("uparrow") == 0)
            Keyboard.KeyUp(System.Windows.Forms.Keys.Up);//KEYCODE.VK_UP);
        else if (gameObject.name.IndexOf("backarrow") == 0)
            Keyboard.KeyUp(System.Windows.Forms.Keys.Down);//KEYCODE.VK_DOWN);
    }

    public void triggerOffArrowIfCurrentCollisionExist()
    {
        if(nbcollisions>0)
        {
            nbcollisions = 0;
            triggerOffArrow();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
