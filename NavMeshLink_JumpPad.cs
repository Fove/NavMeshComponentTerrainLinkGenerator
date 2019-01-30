using UnityEngine;
using System.Collections;
using UnityEngine.AI;

public class NavMeshLink_JumpPad : MonoBehaviour
{
	public NavMeshLink Linker;
	public BoxCollider Trigger;
	public bool IsJumpUp;

	public void Init(int agent, int upperArea,Vector3 hitPos, Vector3 pos, float wid)
	{
		transform.position = pos;
		Linker.agentTypeID = agent;
		Linker.width = wid;
		Linker.startPoint = Vector3.zero;
		Linker.endPoint = hitPos - pos;
		IsJumpUp = Linker.endPoint.y > 0;
		Linker.area = IsJumpUp ? upperArea : 0;

		var fwd = Linker.endPoint;
		fwd.y = 0;
		Trigger.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
		Linker.transform.rotation = Quaternion.identity;
		var size = Trigger.size;
		size.x = Linker.width;
		Trigger.size = size;
		Linker.UpdateLink();
	}

}
