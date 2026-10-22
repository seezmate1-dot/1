using System.Collections.Generic;

namespace SunnyValley.Core.Meta
{
    // Chapter 1 «Восстановление» (GDD 5): Resources/Meta/chapter1.json, written by hand (story + tasks).

    public sealed class ChapterDef
    {
        public string Id;
        public string Title;
        /// <summary>Scene played on the very first launch.</summary>
        public string IntroScene;
        /// <summary>Scene played when the last zone is done; then the farm economy opens.</summary>
        public string FinaleScene;
        public Reward FinaleReward = new Reward();
        public List<CharacterDef> Characters = new List<CharacterDef>();
        public List<ZoneDef> Zones = new List<ZoneDef>();
        public List<TaskDef> Tasks = new List<TaskDef>();
        public List<SceneDef> Scenes = new List<SceneDef>();
    }

    public sealed class CharacterDef
    {
        public string Id;
        public string Name;
        public string Description;
    }

    public sealed class ZoneDef
    {
        public string Id;
        public string Name;
        public string Description;
        /// <summary>Zones that must be complete before this one opens.</summary>
        public List<string> Requires = new List<string>();
        /// <summary>Scene when the zone opens (may be null).</summary>
        public string IntroScene;
        /// <summary>Scene when the zone is complete (may be null).</summary>
        public string OutroScene;
        public Reward Reward = new Reward();
    }

    public sealed class TaskDef
    {
        public string Id;
        public string Zone;
        /// <summary>What the button says, e.g. «Починить крыльцо».</summary>
        public string Title;
        public int Stars;
        /// <summary>Tasks of the same chapter that must be done first.</summary>
        public List<string> Requires = new List<string>();
        /// <summary>Empty, or 3 looks to choose from (changeable later).</summary>
        public List<ChoiceDef> Choices = new List<ChoiceDef>();
        /// <summary>The replica(s) after the task.</summary>
        public List<DialogueLine> Lines = new List<DialogueLine>();
        /// <summary>A multi-line scene after the task (may be null).</summary>
        public string Scene;
        public Reward Reward = new Reward();
    }

    public sealed class ChoiceDef
    {
        public string Id;
        public string Name;
    }

    public sealed class DialogueLine
    {
        /// <summary>Character id.</summary>
        public string Who;
        public string Text;
        public override string ToString() => Who + ": " + Text;
    }

    public sealed class SceneDef
    {
        public string Id;
        public string Title;
        public List<DialogueLine> Lines = new List<DialogueLine>();
    }
}
