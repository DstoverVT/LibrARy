using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.UI.BoundsControl;
using Microsoft.MixedReality.Toolkit.Utilities.Solvers;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

using Reference = AppController.Reference;

/**
 * <summary> 
 * Logic for controlling adaptations of an app. 
 * Adaptations include: 
 * (x,y,z) position/orientation, transparency, scale, visibility, and frame of reference.
 * </summary>
 */
public class AdaptationsController : MonoBehaviour
{    
    /** <summary> Current frame of reference. </summary> */
    public Reference CurrentReference { get; set; }
    /** <summary> Components exclusively used by each frame of reference. </summary> */
    private MonoBehaviour[] headComponents;
    private MonoBehaviour[] bodyComponents;
    private MonoBehaviour[] worldComponents;

    /** Game objects obtained in logic. */
    private ObjectManipulator scaling;
    private SolverHandler sH;
    private GameObject content;

    /** <summary> Variables used for head and body-fixed position calculations. </summary> */
    private Vector3 pointerStart;
    private Vector3 offsetStart;
    public Vector3 InitialBodyPos { get; set; }
    public Vector3 InitialBodyRot { get; set; }

    /** <summary> Data obtained from Unity scene. </summary> */
    [SerializeField]
    private Transform cameraTran;

    /** <summary> Logic for intial startup. Runs before any other logic </summary> */
    void Start()
    {
        /* Assign frame of reference component arrays. */
        worldComponents = new MonoBehaviour[1];
        bodyComponents = new MonoBehaviour[2];
        headComponents = new MonoBehaviour[2];

        ObjectManipulator[] om = GetComponents<ObjectManipulator>();
        scaling = om[1];

        worldComponents[0] = om[0];

        headComponents[0] = GetComponent<RadialView>();
        headComponents[1] = GetComponent<PointerHandler>();

        bodyComponents[0] = GetComponent<BodyReference>();
        bodyComponents[1] = om[0];

        sH = GetComponent<SolverHandler>();
        content = this.transform.Find("ContentQuad").gameObject;

        CurrentReference = Reference.Unopened;
    }


    public void appOpened()
    {
        CurrentReference = Reference.None;
    }


    /** <returns> App transparency as a float, 0 is transparency and 1 is opaque. </returns> */
    public float getTransparency()
    {
        MeshRenderer mesh = content.GetComponent<MeshRenderer>();
        return mesh.material.color.a;
    }


    /** 
     * <summary> Change transparency of app. </summary> 
     * 
     * <param name="data"> Data from slider Game Object. </param>
     */
    public void changeTransparency(SliderEventData data)
    {
        MeshRenderer mesh = content.GetComponent<MeshRenderer>();
        Color color = mesh.material.color;

        color.a = data.NewValue;

        mesh.material.color = color;
    }


    /** <summary> Enable relevant world-fixed components in app. </summary> */
    public void worldFixed()
    {
        disableReferenceFrames();
        // Reset solver offset (can be changed in head fixed mode)
        sH.AdditionalOffset = new Vector3(0, 0, 0);
        
        foreach(MonoBehaviour comp in worldComponents)
        {
            comp.enabled = true;
        }

        CurrentReference = Reference.World;
    }


    /** <summary> Enable relevant head-fixed components in app. </summary> */
    public void headFixed()
    {
        disableReferenceFrames();
        sH.UpdateSolvers = true;
        sH.AdditionalOffset = new Vector3(0, 0, 1);

        foreach (MonoBehaviour comp in headComponents)
        {
            comp.enabled = true;
        }

        CurrentReference = Reference.Head;
    }


    /** <summary> Enable relevant body-fixed components in app. </summary> */
    public void bodyFixed()
    {
        disableReferenceFrames();
        sH.AdditionalOffset = new Vector3(0, 0, 0);

        foreach (MonoBehaviour comp in bodyComponents)
        {
            comp.enabled = true;
        }

        CurrentReference = Reference.Body;
        BodyReference body = (BodyReference)bodyComponents[0];
        body.saveBodyOffset();
    }


    /** <summary> Disable all components used for frame of references. </summary> */
    private void disableReferenceFrames()
    {
        foreach (MonoBehaviour c in worldComponents)
        {
            c.enabled = false;
        }
        foreach (MonoBehaviour c in bodyComponents)
        {
            c.enabled = false;
        }
        foreach (MonoBehaviour c in headComponents)
        {
            c.enabled = false;
        }
        BodyReference body = (BodyReference)bodyComponents[0];
        body.resetBodyFix();
    }


    /** 
     * <summary> 
     * Stores position of user's pointer at start of a click of a head-fixed app. 
     * Used in Unity event for start of PointerHandler click.
     * </summary>
     * 
     * <param name="eventData"> Data from Pointer Event, used to get pointer position. </param>
     */
    public void getPointerStart(MixedRealityPointerEventData eventData)
    {
        // Only if head-fixed
        if (GetComponent<RadialView>().enabled)
        {
            // Track position of pointer at start of click
            offsetStart = sH.AdditionalOffset;
            Vector3 pointerPos = eventData.Pointer.Position;
            pointerStart = cameraTran.InverseTransformPoint(pointerPos);
        }
    }


    /** 
     * <summary>
     * Calculates new app position in head-fixed reference.
     * Used in Unity event for dragging a PointerHandler click on an app.
     * </summary>
     */
    public void moveInHeadFixed(MixedRealityPointerEventData eventData)
    {
        if (GetComponent<RadialView>().enabled)
        {
            // Need to track Pointer Position on pointer drag
            // New position should be x and y from Pointer
            float speed = 2; 
            Vector3 pointerPos = eventData.Pointer.Position;
            Vector3 pointerPosCam = cameraTran.InverseTransformPoint(pointerPos);

            // Want to assign the RadialView offsets to the new position
            Vector3 speedFactor = new Vector3(speed, speed, speed);
            Vector3 positionChange = Vector3.Scale(speedFactor, pointerPosCam - pointerStart);
            sH.AdditionalOffset = offsetStart + positionChange;
        }
    }


    /** <param name="enable"> True to enable movement of the app, else False. </param> */
    public void enableMovement(bool enable)
    {
        // Allow scaling
        scaling.enabled = enable;

        // Move in head-fixed
        if (CurrentReference == Reference.Head)
        {
            headComponents[1].enabled = enable;
        }
        // Move in body-fixed
        else if (CurrentReference == Reference.Body)
        {
            bodyComponents[1].enabled = enable;
        }
        // Move in world-fixed
        else
        {
            //worldComponents[0].enabled = enable;
            worldComponents[0].enabled = enable;
        }
    }


    /** <summary> Recenter body-fixed app position based on initial calculation. </summary> */
    public void recenterBody(Vector3 newForward, Vector3 newUp)
    {
        BodyReference body = (BodyReference)bodyComponents[0];
        body.resetBodyFix();

        /* Get rotation from world to new body coordinate (R_wb'). */
        Quaternion newRotation = Quaternion.LookRotation(newForward, newUp);

        /* 
         * New app rotation = R_wb' * InitialBodyRot -> Actually going to lock rotation to user now (had issues when testing).
         * New app position = cameraWorldPos + (R_wb' * InitialBodyPos) 
         */
        //this.transform.rotation = newRotation * Quaternion.Euler(InitialBodyRot);
        this.transform.position = cameraTran.position + (newRotation * InitialBodyPos);

        /* Need to do this after moving app position. It will face the user. */ 
        /* Find vector from app to camera origin. */
        Vector3 faceUserVec = this.transform.position - cameraTran.position; 
        this.transform.rotation = Quaternion.LookRotation(faceUserVec.normalized, Vector3.up);

        body.saveBodyOffset();
    }
}
