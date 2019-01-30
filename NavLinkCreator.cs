//creat a bunch of NavMeshLink prefab
//if it has a box component(can be disabled), box's bounds will taked as creation bounds
//NavMeshLink_JumpPad is a MonoBehaviour inherited from NavMeshLink, 
//sence unity has bug in 'NavMeshAgent.isOnOffMeshLink',JumpPad made a collider for player to detect link
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using System.Threading;
// using Cieo.Game.Core.Utils;

public class NavLinkCreator : MonoBehaviour
{
	public NavMeshLink_JumpPad linkPrefab;

	public float maxLinkHeightU = 3f;
	public float maxLinkHeightD = 3f;
	public float maxLinkWidth = 3f;
	public int UpperArea;
	public LayerMask raycastLayerMask = -1;
	public readonly Queue<paramData> ExecutionQueue = new Queue<paramData>();
	public bool IsStart;

	private Thread m_MainTh;
	private Bounds m_CalcuBounds;
	public string Logs;
	private float m_MaxDis;

	public struct Edge
	{
		public Vector3 Start;
		public Vector3 End;
		public Edge(Vector3 start, Vector3 end)
		{
			Start = start;
			End = end;
		}
		public string GetKey()
		{
			Vector3 point1;
			Vector3 point2;

			if (Start.x < End.x)
			{
				point1 = Start;
				point2 = End;
			}
			else if (Start.x > End.x)
			{
				point2 = Start;
				point1 = End;
			}
			else if (Start.y < End.y)
			{
				point1 = Start;
				point2 = End;
			}
			else if (Start.y > End.y)
			{
				point2 = Start;
				point1 = End;
			}
			else if (Start.z < End.z)
			{
				point1 = Start;
				point2 = End;
			}
			else
			{
				point2 = Start;
				point1 = End;
			}
			return point1.ToString() + point2.ToString();
		}
	}

	[ContextMenu("Create NavmeshLink")]
	public void CreateNav()
	{
		Debug.Log("Getting Triangles..");
		m_MaxDis = Mathf.Sqrt(maxLinkWidth * maxLinkWidth + maxLinkHeightU * maxLinkHeightU);
		ExecutionQueue.Clear();
		m_CalcuBounds = default;
		var box = GetComponent<BoxCollider>();
		if (box)
		{
			m_CalcuBounds.center = transform.position + box.center;
			m_CalcuBounds.extents = box.size * 0.5f;
		}
		var triangles = NavMesh.CalculateTriangulation();
		m_MainTh = new Thread(new ParameterizedThreadStart(calcuNavLink));
		m_MainTh.Start(triangles);
	}
	[ContextMenu("Abort")]
	public void Abort()
	{
		IsStart = false;
		lock (ExecutionQueue)
		{
			ExecutionQueue.Clear();
		}
	}
	Dictionary<int, Dictionary<string, Edge>> m_Edges;
	private void calcuNavLink(object o)
	{
		IsStart = true;
		m_Edges = new Dictionary<int, Dictionary<string, Edge>>();
		NavMeshTriangulation triangles = (NavMeshTriangulation)o;
		Logs = "Triangles:" + triangles.indices.Length + ", Areas:" + triangles.areas.Length
			+ ", Verticles" + triangles.vertices.Length + "\nCalculating outside edges..";
		Thread.Sleep(8);
		int times = 0;
		Dictionary<string, Edge> edges;

		for (int i = 0; i < triangles.indices.Length - 1; i += 3)
		{
			int area = triangles.areas[i / 3];
			if (!m_Edges.TryGetValue(area, out edges))
			{
				edges = new Dictionary<string, Edge>();
				m_Edges.Add(area, edges);
			}
			addEdge(edges, triangles, i, i + 1);
			addEdge(edges, triangles, i + 1, i + 2);
			addEdge(edges, triangles, i + 2, i);
			times += 3;
			if (times > 1000)
			{
				times -= 1000;
				Logs = "Another 1000 edges..";
				if (!IsStart)
				{
					Logs = "Aborted.";
					break;
				}
				Thread.Sleep(8);
			}
		}
		if (IsStart)
		{
			Logs = "create links..";
			var surfaces = NavMeshSurface.activeSurfaces;
			foreach (var sur in surfaces)
			{
				// List<Edge> edges;
				if (!m_Edges.TryGetValue(sur.defaultArea, out edges))
					continue;
				foreach (KeyValuePair<string, Edge> kvp in edges)
				{
					Edge edge = kvp.Value;
					createLink(sur.agentTypeID, sur.defaultArea, edge);
					while (ExecutionQueue.Count > 10)
						Thread.Sleep(8);
					if (!IsStart)
						break;
				}
				if (!IsStart)
				{
					Logs = "Aborted.";
					break;
				}
			}
		}
		if (IsStart)
		{
			Logs += "Completed.";
		}
	}
	/// <summary>
	/// Recording edges, exclude inner edges, can be optimized to hashset with sort(start,end)
	/// </summary>
	/// <param name="edges"></param>
	/// <param name="triangles"></param>
	/// <param name="i"></param>
	/// <param name="j"></param>
	private void addEdge(Dictionary<string, Edge> edges, NavMeshTriangulation triangles, int i, int j)
	{
		var newEdge = new Edge(triangles.vertices[triangles.indices[i]],
			triangles.vertices[triangles.indices[j]]);
		if(m_CalcuBounds!=default)
			if (!m_CalcuBounds.Contains(newEdge.Start) || !m_CalcuBounds.Contains(newEdge.End))
				return;

		string newEdgeKey = newEdge.GetKey();
		if (edges.ContainsKey(newEdgeKey))
		{
			edges.Remove(newEdgeKey);
		}
		else
		{
			edges.Add(newEdgeKey, newEdge);
		}
	}
	private void createLink(int agent, int area, Edge edge)
	{
		var pos = (edge.Start + edge.End) * 0.5f;
		var normal = Vector3.Cross(edge.End - edge.Start, Vector3.up);
		Vector3 startPos = pos;
		Vector3 endPos = startPos + normal.normalized * maxLinkWidth;
		float wid = Vector3.Distance(edge.End, edge.Start);
		lock (ExecutionQueue)
		{
			var param = new paramData(agent, area, startPos, endPos, pos, wid);
			ExecutionQueue.Enqueue(param);
		}
	}
	public struct paramData
	{
		public int Agent;
		public int Area;
		public Vector3 StartPos;
		public Vector3 EndPos;
		public Vector3 Pos;
		public float Wid;
		public paramData(int agent, int area, Vector3 startPos, Vector3 endPos, Vector3 pos, float wid)
		{
			Agent = agent;
			Area = area;
			StartPos = startPos;
			EndPos = endPos;
			Pos = pos;
			Wid = wid;
		}
	}
	/// <summary>
	/// find higher or lower target position, can be optimized to one ray,but I've no time..
	/// </summary>
	/// <param name="data"></param>
	public void Try2Hits(paramData data)
	{
		int agent = data.Agent;
		int area = data.Area;
		Vector3 startPos = data.StartPos;
		Vector3 endPos = data.EndPos - Vector3.up * maxLinkHeightD * 1.1f;
		Vector3 pos = data.Pos;
		float wid = data.Wid;
		if (!tryHitNav(agent, area, startPos, endPos, pos, wid))
		{//try upper
			endPos = data.EndPos + Vector3.up * maxLinkHeightU * 1.1f;
			tryHitNav(agent, area, startPos, endPos, pos, wid);
		}
	}
	private bool tryHitNav(int agent, int area, Vector3 startPos, Vector3 endPos, Vector3 pos, float wid)
	{
		NavMeshHit navMeshHit = default;
		RaycastHit raycastHit = new RaycastHit();
		float mul = 0f;
		var start = endPos;
		start.y = Mathf.Max(startPos.y, endPos.y);
		endPos.y = Mathf.Min(startPos.y, endPos.y);
		Vector3 hor = endPos - startPos;
		hor.y = 0;
		hor = hor.normalized;
		if (Physics.Raycast(start, Vector3.down, out raycastHit, start.y - endPos.y, raycastLayerMask.value,
		QueryTriggerInteraction.Ignore))
		{
			float agentR = NavMesh.GetSettingsByID(agent).agentRadius;

			bool hit = false;
			float delta = 0.1f / maxLinkWidth;
			while (mul > -1f && NavMesh.SamplePosition(raycastHit.point + hor * mul, out navMeshHit, agentR, 1 << area))
			{
				mul -= delta;
				hit = true;
			}
			if (hit)
			{
				mul += delta * (agentR / 0.1f);
				hit = NavMesh.SamplePosition(raycastHit.point + hor * mul, out navMeshHit, agentR, 1 << area);
			}
			if (hit)
			{
				var obj = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(linkPrefab.gameObject);
				obj.transform.SetParent(transform);
				obj.GetComponent<NavMeshLink_JumpPad>().Init(agent, UpperArea, navMeshHit.position, pos, wid);
				return true;
			}
		}
		return false;
	}
}
#if UNITY_EDITOR

[UnityEditor.CustomEditor(typeof(NavLinkCreator))]
[UnityEditor.CanEditMultipleObjects]
public class NavLinkCreator_Editor : UnityEditor.Editor
{
	NavLinkCreator m_Target;
	private void OnSceneGUI()
	{
		if (!m_Target)
			m_Target = target as NavLinkCreator;
		lock (m_Target.ExecutionQueue)
		{
			while (m_Target.ExecutionQueue.Count > 0)
			{
				if (!m_Target.IsStart)
					break;
				m_Target.Try2Hits(m_Target.ExecutionQueue.Dequeue());
			}
		}
		if (!string.IsNullOrEmpty(m_Target.Logs))
		{
			Debug.Log(m_Target.Logs);
			m_Target.Logs = string.Empty;
		}
	}
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();
		if (GUILayout.Button("Generate"))
		{
			m_Target.CreateNav();
		}

		if (GUILayout.Button("ClearLinks"))
		{
			foreach (var targ in targets)
			{
				Queue<Transform> chs = new Queue<Transform>();
				foreach (Transform ch in m_Target.transform)
				{
					chs.Enqueue(ch);
				}
				while (chs.Count > 0)
				{
					DestroyImmediate(chs.Dequeue().gameObject);
				}
			}
		}
	}
}
#endif
