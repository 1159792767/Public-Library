using Team.DesktopUpdater;
using System.Security.Cryptography;
using System.IO.Compression;
var root=Path.Combine(Path.GetTempPath(),"rollback-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
int checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
void Reject(Action a,string name){try{a();}catch(InvalidDataException){Check(true,name);return;}throw new Exception("Accepted "+name);}
try {
 var a=Path.Combine(root,"v1","App.exe");Directory.CreateDirectory(Path.GetDirectoryName(a)!);File.WriteAllText(a,"old");
 var b=Path.Combine(root,"v2","App.exe");Directory.CreateDirectory(Path.GetDirectoryName(b)!);File.WriteAllText(b,"new");
 var store=new VersionStore(Path.Combine(root,"state"));
 store.Register("1.0.0",a);store.Register("1.0.1",b);
 Check(store.Candidates("1.0.1").Single().Version=="1.0.0","previous version available");
 Check(new VersionStore(Path.Combine(root,"state")).Candidates("1.0.1").Count==1,"history persists");
 var old=store.Candidates("1.0.1").Single();store.Validate(old);Check(true,"offline rollback target verified");
 File.WriteAllText(a,"tampered");Reject(()=>store.Validate(old),"changed binary rejected");
 Reject(()=>store.Validate(new InstalledVersion("0.0.1",b,"fake")),"unregistered target rejected");
 store.Register("1.0.0",a);Check(store.Candidates("1.0.1").Count==1,"duplicate version registration replaced");
 File.Delete(a);Check(store.Candidates("1.0.1").Count==0,"missing target omitted");
 var zip=Path.Combine(root,"bad.zip");using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)){using var w=new StreamWriter(z.CreateEntry("../escape.exe").Open());w.Write("bad");}
 Reject(()=>PackageSafety.Extract(zip,Path.Combine(root,"extract")),"zip traversal rejected");
 Reject(()=>PackageSafety.Verify(zip,new string('0',64)),"invalid digest rejected");
 Reject(()=>PackageSafety.ValidateEntry("../App.exe"),"entry traversal rejected");
 Check(PackageSafety.IsNewer("1.2.0","1.1.1"),"upgrade accepted");
 Check(!PackageSafety.IsNewer("1.0.0","1.1.1"),"feed downgrade refused");
} finally{Directory.Delete(root,true);}
Console.WriteLine($"{checks} checks passed");
