using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
namespace KMep.Supply {
 public struct V {
  public double X,Y,Z;public V(double x,double y,double z){X=x;Y=y;Z=z;}
  public static V operator +(V a,V b){return new V(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}public static V operator -(V a,V b){return new V(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}public static V operator *(V a,double k){return new V(a.X*k,a.Y*k,a.Z*k);}public double Dot(V b){return X*b.X+Y*b.Y+Z*b.Z;}public double Length{get{return Math.Sqrt(Dot(this));}}public double Distance(V b){return (this-b).Length;}public double At(int i){return i==0?X:i==1?Y:Z;}
 }
 public sealed class Box {
  public V Min,Max;public long Owner;public Box(V min,V max,long owner=0){Min=min;Max=max;Owner=owner;}
  public Box Expand(double r){var q=new V(r,r,r);return new Box(Min-q,Max+q,Owner);}
  public bool Hits(V a,V b){double low=0,high=1;for(int i=0;i<3;i++){double x=a.At(i),dx=b.At(i)-x,lo=Min.At(i),hi=Max.At(i);if(Math.Abs(dx)<1e-9){if(x<lo||x>hi)return false;}else{double t=(lo-x)/dx,u=(hi-x)/dx;if(t>u){double s=t;t=u;u=s;}low=Math.Max(low,t);high=Math.Min(high,u);if(low>high)return false;}}return true;}
 }
 public sealed class PlanOptions {public double Radius=25,Padding=2000,MinLeg=120;public int MaxStates=180000,TimeoutMs=7000;public bool Chamfer=true;}
 public sealed class PlanResult {public List<V> Points;public int Expanded;public double Length;public string Reason;public bool Success{get{return Points!=null;}}}
 public static class Planner {
  struct Key:IEquatable<Key>{public int X,Y,Z,D,Stage;public bool Equals(Key b){return X==b.X&&Y==b.Y&&Z==b.Z&&D==b.D&&Stage==b.Stage;}public override bool Equals(object o){return o is Key&&Equals((Key)o);}public override int GetHashCode(){unchecked{return ((((X*397)^Y)*397^Z)*397^D)*397^Stage;}}}
  sealed class Entry {public Key K;public double G,F;}
  sealed class Heap {readonly List<Entry> q=new List<Entry>();public int Count{get{return q.Count;}}public void Push(Entry e){q.Add(e);int i=q.Count-1;while(i>0){int p=(i-1)/2;if(q[p].F<=e.F)break;q[i]=q[p];i=p;}q[i]=e;}public Entry Pop(){var r=q[0];var e=q[q.Count-1];q.RemoveAt(q.Count-1);if(q.Count==0)return r;int i=0;while(i*2+1<q.Count){int c=i*2+1;if(c+1<q.Count&&q[c+1].F<q[c].F)c++;if(q[c].F>=e.F)break;q[i]=q[c];i=c;}q[i]=e;return r;}}
  static readonly V[] Dirs={new V(1,0,0),new V(-1,0,0),new V(0,1,0),new V(0,-1,0),new V(0,0,1),new V(0,0,-1)};
  static V Point(Key k,double[][] c){return new V(c[0][k.X],c[1][k.Y],c[2][k.Z]);}
  static bool Clear(V a,V b,IList<Box> boxes){return !boxes.Any(o=>o.Hits(a,b));}
  static double Heuristic(V p,int stage,IList<V> goals){double v=0;for(int i=stage;i<goals.Count;i++){v+=p.Distance(goals[i]);p=goals[i];}return v;}
  public static PlanResult Find(V start,V goal,V startDirection,V endDirection,IList<Box> obstacles,IList<V> guides,PlanOptions options,Func<V,V,bool> preciseClear=null){
   var timer=Stopwatch.StartNew();var goals=guides.Concat(new[]{goal}).ToList();var boxes=obstacles.Select(b=>b.Expand(options.Radius)).ToList();
   var seeds=new[]{start,goal}.Concat(guides).ToList();double[][] c=new double[3][];
   for(int axis=0;axis<3;axis++){
    double lo=seeds.Min(p=>p.At(axis))-options.Padding,hi=seeds.Max(p=>p.At(axis))+options.Padding;
    var values=seeds.Select(p=>p.At(axis)).Concat(seeds.SelectMany(p=>new[]{p.At(axis)-options.MinLeg,p.At(axis)+options.MinLeg})).Concat(new[]{lo,hi}).ToList();
    foreach(var b in boxes){if(b.Max.At(axis)<lo||b.Min.At(axis)>hi)continue;values.Add(Math.Max(lo,b.Min.At(axis)-1));values.Add(Math.Min(hi,b.Max.At(axis)+1));}
    var sorted=values.Where(x=>x>=lo&&x<=hi).OrderBy(x=>x).ToList();var compact=new List<double>();foreach(double x in sorted)if(compact.Count==0||x-compact.Last()>.01)compact.Add(x);c[axis]=compact.ToArray();
   }
   Func<V,int, int> idx=(p,axis)=>Array.FindIndex(c[axis],x=>Math.Abs(x-p.At(axis))<.02);
   var first=new Key{X=idx(start,0),Y=idx(start,1),Z=idx(start,2),D=Array.FindIndex(Dirs,x=>x.Dot(startDirection)>.9999),Stage=0};
   int end=Array.FindIndex(Dirs,x=>x.Dot(endDirection)>.9999);if(first.D<0||end<0)return new PlanResult{Reason="Port direction is not aligned with planner frame"};
   var open=new Heap();var scores=new Dictionary<Key,double>();var previous=new Dictionary<Key,Key>();var edgeCache=new Dictionary<string,bool>();scores[first]=0;open.Push(new Entry{K=first,G=0,F=Heuristic(start,0,goals)});int expanded=0;
   while(open.Count>0){
    if(++expanded>options.MaxStates||timer.ElapsedMilliseconds>options.TimeoutMs)return new PlanResult{Expanded=expanded,Reason="Giới hạn tìm đường: hãy thêm điểm dẫn hoặc tăng phạm vi."};
    var entry=open.Pop();var k=entry.K;double score;if(!scores.TryGetValue(k,out score)||entry.G>score+.001)continue;V p=Point(k,c);
    int stage=k.Stage;while(stage<goals.Count&&p.Distance(goals[stage])<.02)stage++;
    if(stage==goals.Count&&Dirs[k.D].Dot(endDirection)>=-.001){
     var pts=new List<V>{p};Key cursor=k;while(previous.ContainsKey(cursor)){cursor=previous[cursor];pts.Add(Point(cursor,c));}pts.Reverse();pts=Simplify(pts);
     if(options.Chamfer&&guides.Count==0)pts=Chamfer(pts,startDirection,endDirection,boxes,options.MinLeg);
     return new PlanResult{Points=pts,Expanded=expanded,Length=pts.Zip(pts.Skip(1),(a,b)=>a.Distance(b)).Sum(),Reason="PASS"};
    }
    if(stage==goals.Count)continue;
    for(int direction=0;direction<6;direction++){
     if(Dirs[direction].Dot(Dirs[k.D])<-.001)continue;
     // A visibility edge may cross intermediate grid planes. Keeping a whole
     // straight run in one edge lets us enforce real fitting takeoff lengths.
     int axis=direction/2,sign=direction%2==0?1:-1;int coordinate=axis==0?k.X:axis==1?k.Y:k.Z;
     for(int next=coordinate+sign;next>=0&&next<c[axis].Length;next+=sign){
      var n=new Key{X=k.X,Y=k.Y,Z=k.Z,D=direction,Stage=stage};if(axis==0)n.X=next;else if(axis==1)n.Y=next;else n.Z=next;
      V q=Point(n,c);double length=p.Distance(q);if(length<options.MinLeg-.01)continue;if(!Clear(p,q,boxes)){
       if(preciseClear==null)break;string edge=k.X+","+k.Y+","+k.Z+":"+n.X+","+n.Y+","+n.Z;bool clear;if(!edgeCache.TryGetValue(edge,out clear)){clear=preciseClear(p,q);edgeCache[edge]=clear;}if(!clear)break;
      }
      double cost=score+length+(direction==k.D?0:options.MinLeg*.6);double old;if(scores.TryGetValue(n,out old)&&old<=cost)continue;
      double finalBend=stage==goals.Count-1&&q.Distance(goal)<.02&&Dirs[direction].Dot(endDirection)<.999?options.MinLeg*.6:0;
      scores[n]=cost;previous[n]=k;open.Push(new Entry{K=n,G=cost,F=cost+Heuristic(q,stage,goals)+finalBend});
     }
    }
   }
   return new PlanResult{Expanded=expanded,Reason="Không có tuyến đủ khoảng trống trong phạm vi tìm kiếm."};
  }
  public static List<V> Simplify(List<V> input){var r=new List<V>();foreach(var p in input){if(r.Count>0&&p.Distance(r.Last())<.01)continue;while(r.Count>=2){V a=r[r.Count-1]-r[r.Count-2],b=p-r.Last();if(a.Dot(b)<0||a.Dot(b)/(a.Length*b.Length)<.999999)break;r.RemoveAt(r.Count-1);}r.Add(p);}return r;}
  static List<V> Chamfer(List<V> p,V sd,V ed,IList<Box> boxes,double min){var r=new List<V>{p[0]};for(int i=1;i<p.Count-1;i++){V a=p[i]-p[i-1],b=p[i+1]-p[i];double trim=Math.Min(a.Length,b.Length)*.22;if(trim>=min&&a.Dot(b)/(a.Length*b.Length)<.001){V x=p[i]-a*(trim/a.Length),y=p[i]+b*(trim/b.Length);if(Clear(x,y,boxes)){r.Add(x);r.Add(y);continue;}}r.Add(p[i]);}r.Add(p.Last());return r;}
 }
}
