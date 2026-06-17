using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Rendering;
using Unity.GraphToolkit.Editor;

public class PlayerPlatform : MonoBehaviour
{

    //TUNING VARIABLES
    [Header("Draw Tuning")]
    public float maxInk = 100f; //CombatInput must be able to access and modify this
    [SerializeField] private float maxDrawLength = 10f; // Max length of the platform in World Units
    private float minPointDistance = 0.1f; // Minimum distance between points to be added to the line
    [SerializeField] private float inkLifeTime = 5f; // Time in seconds before the drawn platform disappears
    public struct DrawnPoint //CombatInput must be able to create and add these to the drawnPoints list
    {
        public Vector2 position;
        public float timeCreated;
    }

    //REFERENCES
    [Header("References")]
    [SerializeField] private LineRenderer drawLineRender;
    [SerializeField] private EdgeCollider2D lineCollider; 
    [SerializeField] private LayerMask playerLayer; //What the drawn points will collide with
    private CombatInput cIScript;

    //Internal variables
    private Queue<DrawnPoint> drawnPoints; //List of current element being drawn
    private List<Vector2> drawnPointsLst;
    [SerializeField] private int pointCount = 10;
    public float currentInkUsed = 0; //Current ink used for the current line, CombatInput must be able to access this
    private float currentDrawLength = 0;
    private bool ableToDraw = true; //Variable to check if the player can draw, set to false when they run out of ink or time or wtv else

    //Function for starting the draw process, called by CombatInput when the player starts drawing
    public void StartDraw()
    {
        drawnPoints.Clear();
        currentInkUsed = 0;
        currentDrawLength = 0;
        drawLineRender.enabled = true;
        lineCollider.enabled = true;
    }

    public void UpdateDraw(Vector2 newPoint)
    {
        if (!ableToDraw) return;

        if (currentDrawLength >= maxDrawLength) return; //Code to check drawn length
        float segmentLength = drawnPoints.Count > 0 ? Vector2.Distance(drawnPoints.Peek().position, newPoint) : 0f;
        if (drawnPoints.Count > 0 && segmentLength < minPointDistance) return; //Code to check distance between points
        Vector2 lastPoint = drawnPoints.Count > 0 ? drawnPoints.Peek().position : newPoint;
        RaycastHit2D hit = Physics2D.Linecast(lastPoint, newPoint, playerLayer);
        if (hit.collider != null)
        {
            ableToDraw = false; //Stop the player from drawing if they try to draw through themselves
            return;
        }
        if (cIScript.currentDrawInk + segmentLength > cIScript.maxDrawInk) return; //Code to check ink usage, if the next segment would put the player over the max ink, don't add it
        currentInkUsed += segmentLength;
        cIScript.currentDrawInk += segmentLength; //Update the CombatInput script's current ink variable
        currentDrawLength += segmentLength;
        DrawnPoint newDrawnPoint = new DrawnPoint { position = newPoint, timeCreated = Time.time };
        drawnPoints.Enqueue(newDrawnPoint);
        UpdateDrawnVisuals();
    }

    private void UpdateDrawnVisuals()
    {
        //Update LineRenderer
        drawLineRender.positionCount = drawnPoints.Count;
        drawnPointsLst.Clear();
        int i = 0; 
        foreach (DrawnPoint point in drawnPoints)
        {
            drawLineRender.SetPosition(i, new Vector3(point.position.x, point.position.y, 0));
            drawnPointsLst.Add(point.position);
            i++;

        }
        //Update EdgeCollider2D
        if (i > 1)
        {
            lineCollider.enabled = true;
            lineCollider.SetPoints(drawnPointsLst);
        }
        else lineCollider.enabled = false;
    }

    private void FadeOldPlatformInk()
    {
        if (drawnPoints.Count == 0)
        {
            cIScript.StartCoroutine("returnInk", currentInkUsed);
            Destroy(gameObject);
            return;
        }
        bool pointsRemoved = false;
        while (drawnPoints.Count > 0 && Time.time - drawnPoints.Peek().timeCreated >= inkLifeTime)
        {
            drawnPoints.Dequeue();
            pointsRemoved = true;
        }
        if (pointsRemoved) UpdateDrawnVisuals();
        if( drawnPoints.Count == 0)
        {
            drawLineRender.enabled = false;
            lineCollider.enabled = false;
            cIScript.StartCoroutine("returnInk", currentInkUsed);
            Destroy(gameObject);
        }
    }

    //Function for resetting the draw, called by CombatInput when the player finishes drawing or runs out of ink/time
    public void ResetDraw()
    {

    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        drawnPoints = new Queue<DrawnPoint>(pointCount);
        drawnPointsLst = new List<Vector2>(pointCount);
    }

    void Start()
    {
        cIScript = GameObject.FindAnyObjectByType<CombatInput>();

        if (cIScript == null)
        {
            Debug.LogError("Platform could not find the CombatInput script in the scene!");
        }
    }

    // Update is called once per frame
    void Update()
    {
        FadeOldPlatformInk();
    }
}
