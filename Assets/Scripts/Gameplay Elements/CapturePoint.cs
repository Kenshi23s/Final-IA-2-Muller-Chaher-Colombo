using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(DebugableObject))]
public class CapturePoint : MonoBehaviour
{
    public enum ZoneStates
    {
        Disputed,
        Neutral,
        BeingTaken,
        Taken
    }

    [SerializeField, Min(1)] float _waitingTimeTilSearch = 0.2f;
    [SerializeField, Min(0)] float _zoneRadius = 15, _zoneHeight = 8;
    DebugableObject _debug;

    #region Events
    public UnityEvent onPointOwnerChange;

    public UnityEvent onProgressChange;

    public UnityEvent<Dictionary<Team, Entity[]>> onEntitiesAroundUpdate;

    public event Action<Team> onCaptureComplete;
    #endregion

    public float TakeProgress { get; private set; }


    [SerializeField]
    SpatialGrid3D _targetGrid;

    [field: SerializeField] public float ProgressRequiredForCapture { get; private set; }
    float captureProgress = 0;

    public float CaptureProgress
    {
        get => captureProgress;
        private set
        {
            captureProgress = Mathf.Clamp(value, -ProgressRequiredForCapture, ProgressRequiredForCapture);
        }
    }

    public float ZoneProgressNormalized 
    {
        get
        {                  
             return captureProgress / (ProgressRequiredForCapture * 2) + 0.5f;          
               
        }
    }
        
  
    public ZoneStates CurrentCaptureState { get; private set; }
    
    public Team takenBy = Team.None;
    public Team beingTakenBy { get; private set; }

    Dictionary<Team, Entity[]> _teamSplit = new Dictionary<Team, Entity[]> 
    {
        [Team.Blue] = new Entity[0],
        [Team.Red]  = new Entity[0]
    };

    void Awake()
    {
        _debug = GetComponent<DebugableObject>();
        _debug.AddGizmoAction(DrawRadius);

        captureProgress = 0;
    }


    private void Start()
    {
        _targetGrid = FindObjectOfType<SpatialGrid3D>();
        CapturePointManager.instance.AddZone(this);
        StartCoroutine(SearchEntitiesAround());
    }

  
    IEnumerator SearchEntitiesAround()
    {
        while (true) 
        {
            //divido la lista entre equipo rojo y verde con el lookup
            //si pasan el predicado, accedo a esos items con [true] y si no
            //accedo a los otros items con [false]

            _teamSplit = ZoneQuery()
                .OfType<Entity>()
                .Where(x => x.MyTeam != Team.None)
                .ToLookup(x => x.MyTeam)
                .ToDictionary(x => x.Key, x => x.ToArray());

            foreach (Team item in Enum.GetValues(typeof(Team)))
            {
                if (!_teamSplit.ContainsKey(item))
                    _teamSplit[item] = new Entity[0];
            }



            onEntitiesAroundUpdate?.Invoke(_teamSplit);
            yield return new WaitForSeconds(_waitingTimeTilSearch);
        }    
    }

    private void Update()
    {
        if (!_teamSplit[Team.Red].Any() && !_teamSplit[Team.Blue].Any())
        {
            _debug.Log("No hay unidades en el area");
            return;
        }


        //rojo                    
        if (_teamSplit[Team.Red].Any() && _teamSplit[Team.Blue].Any())
        {
            CurrentCaptureState = ZoneStates.Disputed;
            _debug.Log("Esta en disputa, hay unidades de ambos equipos");
            return;
        }

        string debug = "Esta siendo tomada por el equipo";

        if (_teamSplit[Team.Red].Any() && takenBy != Team.Red)
        {
            debug += " rojo";
            CaptureProgress += Time.deltaTime * _teamSplit[Team.Red].Count();
            beingTakenBy = Team.Red;
        }
        else if (_teamSplit[Team.Blue].Any() && takenBy != Team.Blue)
        {
            debug += " azul";
            CaptureProgress -= Time.deltaTime * _teamSplit[Team.Blue].Count();
            beingTakenBy = Team.Blue;
        }
        debug += $" el progreso es de {captureProgress}";
        _debug.Log(debug);

        onProgressChange?.Invoke();
        CheckCaptureProgress();
    }

    void CheckCaptureProgress()
    {
        var aux = beingTakenBy;


        switch (beingTakenBy)
        {
            case Team.Red:
                if (captureProgress >= ProgressRequiredForCapture && takenBy != Team.Red )
                {
                    Debug.Log("tomada por el rojo, invoco evento de captura completada");
                    takenBy = Team.Red;
                    onCaptureComplete?.Invoke(takenBy);
                    onCaptureComplete = delegate { };
                }
                    
                break;
            case Team.Blue:
                if (captureProgress <= -ProgressRequiredForCapture && takenBy != Team.Blue)
                {
                    Debug.Log("tomada por el rojo, invoco evento de captura completada");
                    takenBy = Team.Blue;
                    onCaptureComplete?.Invoke(takenBy);
                    onCaptureComplete = delegate { };
                }
                    
                break;
            default:
                takenBy = Team.None;
                break;
        }

        if (takenBy != aux) onPointOwnerChange?.Invoke();
    }

    private void DrawRadius()
    {
        Gizmos.color = new Color(243, 58, 106, 255) / 255;

        DrawCylinder(transform.position, Quaternion.identity, _zoneHeight, _zoneRadius);

    }

    IEnumerable<GridEntity> ZoneQuery() 
    {
        //creo una "caja" con las dimensiones deseadas, y luego filtro segun distancia para formar el c�rculo
        return _targetGrid.Query(
            transform.position + new Vector3(-_zoneRadius, 0, -_zoneRadius),
            transform.position + new Vector3(_zoneRadius, _zoneHeight, _zoneRadius),
            pos => {
                var distance = pos - transform.position;
                distance.y = transform.position.y;
                return distance.sqrMagnitude < _zoneRadius * _zoneRadius;
            });
    }

    public static void DrawCylinder(Vector3 position, Quaternion orientation, float height, float radius, bool drawFromBase = true)
    {
        Vector3 localUp = orientation * Vector3.up;
        Vector3 localRight = orientation * Vector3.right;
        Vector3 localForward = orientation * Vector3.forward;

        Vector3 basePositionOffset = drawFromBase ? Vector3.zero : (localUp * height * 0.5f);
        Vector3 basePosition = position - basePositionOffset;
        Vector3 topPosition = basePosition + localUp * height;

        Quaternion circleOrientation = orientation * Quaternion.Euler(90, 0, 0);

        Vector3 pointA = basePosition + localRight * radius;
        Vector3 pointB = basePosition + localForward * radius;
        Vector3 pointC = basePosition - localRight * radius;
        Vector3 pointD = basePosition - localForward * radius;

        Gizmos.DrawRay(pointA, localUp * height);
        Gizmos.DrawRay(pointB, localUp * height);
        Gizmos.DrawRay(pointC, localUp * height);
        Gizmos.DrawRay(pointD, localUp * height);

        DrawCircle(basePosition, circleOrientation, radius, 32);
        DrawCircle(topPosition, circleOrientation, radius, 32);
    }

    public static void DrawCircle(Vector3 position, Quaternion rotation, float radius, int segments)
    {
        // If either radius or number of segments are less or equal to 0, skip drawing
        if (radius <= 0.0f || segments <= 0)
        {
            return;
        }

        // Single segment of the circle covers (360 / number of segments) degrees
        float angleStep = (360.0f / segments);

        // Result is multiplied by Mathf.Deg2Rad constant which transforms degrees to radians
        // which are required by Unity's Mathf class trigonometry methods

        angleStep *= Mathf.Deg2Rad;

        // lineStart and lineEnd variables are declared outside of the following for loop
        Vector3 lineStart = Vector3.zero;
        Vector3 lineEnd = Vector3.zero;

        for (int i = 0; i < segments; i++)
        {
            // Line start is defined as starting angle of the current segment (i)
            lineStart.x = Mathf.Cos(angleStep * i);
            lineStart.y = Mathf.Sin(angleStep * i);
            lineStart.z = 0.0f;

            // Line end is defined by the angle of the next segment (i+1)
            lineEnd.x = Mathf.Cos(angleStep * (i + 1));
            lineEnd.y = Mathf.Sin(angleStep * (i + 1));
            lineEnd.z = 0.0f;

            // Results are multiplied so they match the desired radius
            lineStart *= radius;
            lineEnd *= radius;

            // Results are multiplied by the rotation quaternion to rotate them 
            // since this operation is not commutative, result needs to be
            // reassigned, instead of using multiplication assignment operator (*=)
            lineStart = rotation * lineStart;
            lineEnd = rotation * lineEnd;

            // Results are offset by the desired position/origin 
            lineStart += position;
            lineEnd += position;

            // Points are connected using DrawLine method and using the passed color
            Gizmos.DrawLine(lineStart, lineEnd);
        }
    }
}
