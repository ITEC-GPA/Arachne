using System;
using System.Collections.Generic;
using System.Linq;

namespace Arachne.Core
{
    public sealed class ModelCheckReport
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public int ConnectedParts { get; internal set; }
        public bool Valid => Errors.Count==0;
    }
    public static class ModelChecks
    {
        public static ModelCheckReport Check(FormModel model)
        {
            if(model==null)throw new ArgumentNullException(nameof(model));
            var r=new ModelCheckReport();var adjacent=model.Nodes.Select(_=>new List<int>()).ToArray();
            var edgeMap=new HashSet<(int,int)>();
            foreach(var e in model.Edges)
            {
                adjacent[e.Start].Add(e.End);adjacent[e.End].Add(e.Start);
                if(!edgeMap.Add(Key(e.Start,e.End)))r.Errors.Add($"Duplicate cable {e.Id}.");
                if(Validation.Distance(model.Nodes[e.Start].Position,model.Nodes[e.End].Position)<=model.Tolerance)r.Errors.Add($"Collapsed cable {e.Id}.");
            }
            var visited=new HashSet<int>();
            for(int i=0;i<model.Nodes.Count;i++)
            {
                if(!visited.Add(i))continue;var queue=new Queue<int>();queue.Enqueue(i);var part=new List<int>();
                while(queue.Count>0){int a=queue.Dequeue();part.Add(a);foreach(int b in adjacent[a])if(visited.Add(b))queue.Enqueue(b);}
                r.ConnectedParts++;
                if(!part.Any(n=>model.Nodes[n].FixX))r.Errors.Add($"Connected part {r.ConnectedParts}: no X restraint (translation mechanism).");
                if(!part.Any(n=>model.Nodes[n].FixY))r.Errors.Add($"Connected part {r.ConnectedParts}: no Y restraint (translation mechanism).");
                if(!part.Any(n=>model.Nodes[n].FixZ))r.Errors.Add($"Connected part {r.ConnectedParts}: no Z restraint (translation mechanism).");
            }
            if(r.ConnectedParts>1)r.Warnings.Add($"Model contains {r.ConnectedParts} disconnected parts.");
            var sides=new Dictionary<(int,int),List<(int,int)>>();int faceId=0;
            foreach(var face in model.Faces)
            {
                faceId++;var p=face.Select(i=>model.Nodes[i].Position).ToArray();
                if(ResultMath.Area(p)<=model.Tolerance*model.Tolerance)r.Errors.Add($"Plate {faceId}: zero area.");
                if(face.Length==4)
                {
                    var n1=ResultMath.Cross(ResultMath.Difference(p[1],p[0]),ResultMath.Difference(p[2],p[0]));
                    var n2=ResultMath.Cross(ResultMath.Difference(p[2],p[0]),ResultMath.Difference(p[3],p[0]));
                    if(n1.X*n2.X+n1.Y*n2.Y+n1.Z*n2.Z<=0)r.Errors.Add($"Plate {faceId}: folded or self-intersecting quad; triangulate explicitly.");
                }
                for(int j=0;j<face.Length;j++)
                {
                    int a=face[j],b=face[(j+1)%face.Length];var key=Key(a,b);
                    if(!edgeMap.Contains(key))r.Warnings.Add($"Plate {faceId}: side {j+1} has no cable; boundary force unavailable.");
                    if(!sides.TryGetValue(key,out var entries))sides.Add(key,entries=new List<(int,int)>());entries.Add((a,b));
                }
            }
            foreach(var side in sides)
            {
                if(side.Value.Count>2)r.Errors.Add($"Non-manifold side at nodes {side.Key.Item1+1}, {side.Key.Item2+1}.");
                else if(side.Value.Count==2 && side.Value[0]==side.Value[1])r.Errors.Add($"Inconsistent plate orientation at nodes {side.Key.Item1+1}, {side.Key.Item2+1}.");
            }
            r.Warnings.Add("Checks detect connectivity and basic geometry issues; they do not certify nonlinear stability or positive cable tensions.");
            return r;
        }
        private static (int,int) Key(int a,int b)=>(Math.Min(a,b),Math.Max(a,b));
    }
}
