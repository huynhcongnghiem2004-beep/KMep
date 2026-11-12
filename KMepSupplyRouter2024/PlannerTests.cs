using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace KMep.Supply {
 public static class PlannerTests {
  static int count;
  static void Check(string name,bool condition){if(!condition)throw new Exception("FAIL: "+name);count++;}
  static void Verify(string name,PlanResult r,V s,V g,V u,V v,List<Box> boxes,PlanOptions o){
   Check(name+" found",r.Success);Check(name+" endpoints",r.Points.First().Distance(s)<.02&&r.Points.Last().Distance(g)<.02);
   V prev=u;for(int i=1;i<r.Points.Count;i++){V delta=r.Points[i]-r.Points[i-1];double dot=prev.Dot(delta)/delta.Length;Check(name+" fitting angle",Math.Abs(dot-1)<1e-5||Math.Abs(dot)<1e-5||Math.Abs(dot-Math.Sqrt(.5))<1e-5);Check(name+" clearance",!boxes.Any(b=>b.Expand(o.Radius).Hits(r.Points[i-1],r.Points[i])));prev=delta*(1/delta.Length);}Check(name+" final bend",prev.Dot(v)>=-.0001);
  }
  public static void Main(){
   var o=new PlanOptions{TimeoutMs=15000};var none=new List<Box>();V x=new V(1,0,0),y=new V(0,1,0),z=new V(0,0,1),s=new V(0,0,0);
   var dirs=new[]{x,x*-1,y,y*-1,z,z*-1};int scenario=0;
   foreach(var u in dirs)foreach(var v in dirs){var g=new V(1100,1300,900);var r=Planner.Find(s,g,u,v,none,new V[0],o);Verify("orientations "+scenario++,r,s,g,u,v,none,o);}
   var wall=new List<Box>{new Box(new V(400,-500,-500),new V(600,1600,1600))};V target=new V(1400,1200,800);
   Verify("detour wall",Planner.Find(s,target,x,y,wall,new V[0],o),s,target,x,y,wall,o);
   var shaft=new List<Box>{new Box(new V(300,-600,-600),new V(700,1600,1600)),new Box(new V(-600,300,-600),new V(1600,700,1600))};
   Verify("3D congested",Planner.Find(s,new V(1400,1400,1400),z,x,shaft,new V[0],o),s,new V(1400,1400,1400),z,x,shaft,o);
   var guide=new V(0,-1000,1000);var guided=Planner.Find(s,target,x,y,wall,new[]{guide},o);Verify("guide",guided,s,target,x,y,wall,o);Check("mandatory guide",guided.Points.Any(p=>p.Distance(guide)<.02));
   var blocked=new List<Box>{new Box(new V(-100,-100,-100),new V(100,100,100))};Check("enclosed rejected",!Planner.Find(s,target,x,y,blocked,new V[0],o).Success);
   Check("time/state bound",!Planner.Find(s,target,x,y,wall,new V[0],new PlanOptions{MaxStates=1}).Success);
   var rand=new Random(421);for(int i=0;i<40;i++){var g=new V(500+rand.Next(1800),500+rand.Next(1800),500+rand.Next(1800));Verify("random "+i,Planner.Find(s,g,dirs[i%6],dirs[(i+2)%6],none,new V[0],o),s,g,dirs[i%6],dirs[(i+2)%6],none,o);}
   File.WriteAllText("planner-test-result.txt","PASS "+count+" assertions, "+(scenario+45)+" scenarios; "+DateTime.Now.ToString("O"));Console.WriteLine("PASS "+count+" assertions");
  }
 }
}
