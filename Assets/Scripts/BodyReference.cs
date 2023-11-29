using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/** <summary> Body-fixed app calculations component. </summary> */
public class BodyReference : MonoBehaviour
{
    /** <summary> Obtained from Unity editor. </summary> */
    public GameObject player;

    /** <summary> Calculate offset from user's head. </summary> */
    private Vector3 startOffset;

    private bool startUpdating = false;

    /** 
     * <summary> 
     * Saves offset of app from user's head at this moment. 
     * Also called as Unity event when app is moved during edit mode.
     * </summary>
     */
    public void saveBodyOffset()
    {
        if (this.enabled)
        {
            //AppController.debugAndLog("save body");
            startUpdating = true;
            startOffset = this.transform.position - player.transform.position;
        }
    }


    /** <summary> 
     * Once set, always add initial offset to current position to get "body-fixed" position.
     * This only works if the initial offset is correct, which is recalculated when user changes direction
     * in AppController Update()
     * </summary> */
    private void Update()
    {
        if (startUpdating)
        {
            this.transform.position = player.transform.position + startOffset;
        }
    }


    /** 
     * <summary> 
     * Reset body-fixed calculation. 
     * Also called as Unity event when app is moved during edit mode.
     * </summary> 
     */
    public void resetBodyFix()
    {
        //AppController.debugAndLog("reset body");
        startUpdating = false;
        startOffset = new Vector3();
    }
}
