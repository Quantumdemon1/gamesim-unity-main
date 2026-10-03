using Gamesim.House;
using UnityEngine;

namespace Gamesim.Presentation
{
    /// <summary>
    /// The complete occupied station, including native arrival tolerance and the fitted actor.
    /// Instruments remain collider-free; this conservative reservation keeps their solids away
    /// from furniture, neighbouring actors and other instruments before any route is leased.
    /// </summary>
    public readonly struct CompetitionStageFootprint
    {
        public readonly Vector3 Center, HalfSize;
        public readonly Quaternion Rotation;
        private const float Margin=.04f;

        private CompetitionStageFootprint(Vector3 center,Vector3 halfSize,Quaternion rotation)
        {Center=center;HalfSize=halfSize;Rotation=rotation;}

        public static CompetitionStageFootprint Station(CompetitionApparatus.Family family,Vector3 feet,float facing,
            float radius,float height,float arrival=.25f)
        {
            // The farthest grip solid is its counterweight; height/depth include the fitted
            // shoulder/arm range of the supported humanoids, rather than just the default rig.
            float forward=family==CompetitionApparatus.Family.GripRig?1.25f:1.05f;
            float back=radius+arrival+Margin;
            float front=forward+arrival+Margin;
            float width=Mathf.Max(radius,.51f)+arrival+Margin;
            float top=Mathf.Max(3f,height*1.5f);
            var rotation=Quaternion.Euler(0,facing,0);
            return new CompetitionStageFootprint(feet+rotation*new Vector3(0,(top+.001f)*.5f,(front-back)*.5f),
                new Vector3(width,(top-.001f)*.5f,(front+back)*.5f),rotation);
        }

        public static CompetitionStageFootprint Actor(Vector3 feet,float radius,float height,float arrival=.25f)
            =>new CompetitionStageFootprint(feet+Vector3.up*(height+.001f)*.5f,
                new Vector3(radius+arrival+Margin,(height-.001f)*.5f,radius+arrival+Margin),Quaternion.identity);

        public bool FitsOn(Bounds deck)
        {
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                var corner=Center+Rotation*new Vector3(x*HalfSize.x,0,z*HalfSize.z);
                if(corner.x<deck.min.x+Margin || corner.x>deck.max.x-Margin
                    || corner.z<deck.min.z+Margin || corner.z>deck.max.z-Margin)return false;
            }
            return true;
        }

        public bool Overlaps(CompetitionStageFootprint other)
        {
            if(Mathf.Abs(Center.y-other.Center.y)>=HalfSize.y+other.HalfSize.y)return false;
            Vector3 right=Rotation*Vector3.right,forward=Rotation*Vector3.forward;
            Vector3 otherRight=other.Rotation*Vector3.right,otherForward=other.Rotation*Vector3.forward;
            return !Separated(right,other,right,forward,otherRight,otherForward)
                && !Separated(forward,other,right,forward,otherRight,otherForward)
                && !Separated(otherRight,other,right,forward,otherRight,otherForward)
                && !Separated(otherForward,other,right,forward,otherRight,otherForward);
        }

        private bool Separated(Vector3 axis,CompetitionStageFootprint other,Vector3 right,Vector3 forward,Vector3 otherRight,Vector3 otherForward)
        {
            float a=Mathf.Abs(Vector3.Dot(axis,right))*HalfSize.x+Mathf.Abs(Vector3.Dot(axis,forward))*HalfSize.z;
            float b=Mathf.Abs(Vector3.Dot(axis,otherRight))*other.HalfSize.x+Mathf.Abs(Vector3.Dot(axis,otherForward))*other.HalfSize.z;
            return Mathf.Abs(Vector3.Dot(other.Center-Center,axis))>=a+b;
        }

        public bool HasStaticClearance(PhysicsScene physics,Collider floor,Collider[] scratch)
        {
            // Furniture has its own layer and is deliberately absent from sight queries.
            // Placement instead sees every solid layer, including navigation-only proxies.
            int count=physics.OverlapBox(Center,HalfSize,scratch,Rotation,~0,QueryTriggerInteraction.Ignore);
            if(count==scratch.Length)return false;
            for(int i=0;i<count;i++)
            {
                var hit=scratch[i];
                if(hit==null)return false;
                if(hit==floor || hit.GetComponentInParent<HousePlayerController>()!=null
                    || hit.GetComponentInParent<HouseNpc>()!=null)continue;
                return false;
            }
            return true;
        }

        /// <summary>Checks the actual authored mesh corners after contact fitting, without AABB inflation.</summary>
        public bool Contains(MeshFilter mesh)
        {
            if(mesh==null || mesh.sharedMesh==null)return false;
            var bounds=mesh.sharedMesh.bounds;var inverse=Quaternion.Inverse(Rotation);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var point=mesh.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)));
                var local=inverse*(point-Center);
                if(Mathf.Abs(local.x)>HalfSize.x+.001f || Mathf.Abs(local.y)>HalfSize.y+.001f || Mathf.Abs(local.z)>HalfSize.z+.001f)return false;
            }
            return true;
        }
    }
}
