using System.Linq;
using Multiplayer.API;
using Verse;

namespace ProcessorFramework
{
    /*[StaticConstructorOnStartup]
    static class MultiplayerCompatibility
    {
        static MultiplayerCompatibility()
        {
            if (!MP.enabled) return;

            // Sync all gizmo clicks

            MP.RegisterSyncMethod(typeof(Command_Process), nameof(Command_Process.ChangeProcess)).SetContext(SyncContext.MapSelected);
            MP.RegisterSyncMethod(typeof(Command_Quality), nameof(Command_Quality.ChangeQuality)).SetContext(SyncContext.MapSelected);

            var methods = new[] {
                nameof(ProcessorFramework_Utility.FinishProcess),
                nameof(ProcessorFramework_Utility.ProgressOneDay),
                nameof(ProcessorFramework_Utility.ProgressHalfQuadrum),
                nameof(ProcessorFramework_Utility.EmptyObject),
                nameof(ProcessorFramework_Utility.FillObject),,
            };
            foreach (string methodName in methods) {
                MP.RegisterSyncMethod(typeof(ProcessorFramework_Utility), methodName);
            }

            MP.RegisterSyncWorker<ProcessDef>(UF_Process_SyncWorker, shouldConstruct: false);
        }

        // This is only called whenever user changes process, which is seldom.
        static void UF_Process_SyncWorker(SyncWorker sync, ref ProcessDef obj)
        {
            if (sync.isWriting) {
                sync.Write(obj.uniqueID);
            } else {
                int id = sync.Read<int>();

                obj = ProcessorFramework_Utility.allProcessDefs.First(p => p.uniqueID == id);
            }
        }
    }*/
}