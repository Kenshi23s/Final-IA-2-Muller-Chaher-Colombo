using System.Collections.Generic;
using UnityEngine;
using System;
using FacundoColomboMethods;
using System.Linq;
using static UnityEngine.UI.GridLayoutGroup;

[RequireComponent(typeof(NewPhysicsMovement))]
[RequireComponent(typeof(DebugableObject))]
[DisallowMultipleComponent]
public class NewAIMovement : MonoBehaviour
{
    public NewPhysicsMovement ManualMovement { get; private set; }
    public GridEntity owner { get; private set; }
    public event Action OnDestinationReached, OnDestinationChanged, OnMovementCanceled;
    DebugableObject _debug;

    public float destinationArriveDistance;

    Action _update, _fixedUpdate;

    List<Vector3> _path = new List<Vector3>();

    [SerializeField] LayerMask obstacleMask;

    public Vector3 destination { get; private set; }


    private void Awake()
    {
        ManualMovement = GetComponent<NewPhysicsMovement>();
        owner = GetComponent<GridEntity>();
        _debug = GetComponent<DebugableObject>();
        _debug.AddGizmoAction(DrawPath);
    }

    public void Update() => _update?.Invoke();

    public void SetDestination(Vector3 newDestination)
    {
        if (_path.Any() && newDestination != destination) OnDestinationChanged?.Invoke();

        destination = newDestination;

        if (transform.position.InLineOffSight(newDestination, AI_Manager.instance.wall_Mask))
        {
            OnDesinationAtSight(newDestination);
        }
        else
        {
            CalculatePath(newDestination);
        }
    }

    void OnDesinationAtSight(Vector3 newDestination)
    {
        // Conseguir la posicion en el piso
        if (Physics.Raycast(newDestination, Vector3.down, out RaycastHit hitInfo, 10f, AI_Manager.instance.wall_Mask))
        {
            newDestination = hitInfo.point;
            destination = newDestination;
        }

        _fixedUpdate = () =>
        {
            _debug.Log("Veo el destino, voy directo.");

            if (Vector3.Distance(newDestination, transform.position) < destinationArriveDistance)
            {
                OnDestinationReached?.Invoke();
                ClearPath();
            }
            else
            {
                ManualMovement.AccelerateTowardsTarget(newDestination);
            }
        };
    }

    void CalculatePath(Vector3 newDestination)
    {
        _debug.Log("No veo el destino, calculo el camino.");

        AI_Manager I = AI_Manager.instance;

        Tuple<Node, Node> keyNodes = Tuple.Create(I.GetNearestNode(transform.position), I.GetNearestNode(newDestination));

        if (keyNodes.Item1 != null && keyNodes.Item2 != null)
            StartCoroutine(keyNodes.CalculateLazyThetaStar(I.wall_Mask, OnFinishCalculatingPath, destination, 200));
        else
        {
            string node1 =  keyNodes.Item1 != null ? "NO es null " : "ES null ";
            string node2 =  keyNodes.Item2 != null ? "NO es null " : "ES null ";

            _debug.Log("El nodo INICIAL" + node1+ " y El nodo FINAL" + node2);
        }
            
    }

    void OnFinishCalculatingPath(bool pathmade,List<Vector3> newPath)
    {
        if (!pathmade && !newPath.Any())
        {
            _debug.Log("No se pudo armar el camino");
            return;
        }


        _path = newPath;

        _debug.Log("Arme el camino, lo reproduzo ");
        _fixedUpdate = PlayPath;
    }

    void PlayPath()
    {
        // Si llegamos al waypoint mas cercano, quitarlo para pasar al siguiente
        if (Vector3.Distance(_path[0], transform.position) < destinationArriveDistance)      
            _path.RemoveAt(0);
        

        // Mientras queden waypoints seguir avanzando
        if (_path.Any())
        {
            _debug.Log("Se mueve hacia el siguiente nodo, me faltan " + _path.Count);
            ManualMovement.AccelerateTowardsTarget(_path[0]);
        }
        else // Si no quedan, finalizar el recorrido
        {
            _debug.Log("no hay mas nodos, corto pathfinding");
            OnDestinationReached?.Invoke();
            ClearPath();
            return;
        }

    }

    void ClearPath()
    {
        _fixedUpdate = null;
        ManualMovement.ClearForces();
        ManualMovement.AccelerateTowards(Vector3.zero);
        _path.Clear();
    }

    public void CancelMovement()
    {
       
        ClearPath();
        OnMovementCanceled?.Invoke();
    }

    private void FixedUpdate() => _fixedUpdate?.Invoke();

    void DrawPath()
    {
        if (_path.Any())
            Gizmos.DrawLine(transform.position, _path[0]);

        for (int i = 0; i < _path.Count - 1; i++)
        {
            Gizmos.DrawLine(_path[i], _path[i + 1]);
        }
    }

    Vector3 ObstacleAvoidance()
    {

        float dist = ManualMovement.CurrentSpeed;

        if (Physics.SphereCast(transform.position, 1.5f, transform.forward, out RaycastHit hit, dist, obstacleMask))
        {
            //_debug.Log("ESTOY HACIENDO OBSTACLE AVOIDANCE!");
            Vector3 obstacle = hit.transform.position;
            Vector3 dirToObject = obstacle - transform.position;
            float angleInBetween = Vector3.SignedAngle(transform.forward, dirToObject, Vector3.up);

            Vector3 desired = angleInBetween >= 0 ? -transform.right : transform.right;

            return desired;
        }

        return Vector3.zero;
    }

}
