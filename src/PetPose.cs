namespace DeepSeekWhale
{
    public enum PetAction
    {
        None,
        Bubbles,
        Yawn,
        Stretch,
        Spin,
        Jump,
        Shoot
    }

    public enum PetSkin
    {
        DeepBlue,
        Sakura,
        Mint
    }

    /// <summary>One frame's pose for the whale: idle action, drag, drop, sleep, skin.</summary>
    public class PetPose
    {
        public float React = -1f;
        public PetAction Action = PetAction.None;
        public float ActionT;
        public bool Dragging;
        public float DragTilt;
        public float DropT = -1f;
        public bool Sleeping;
        public PetSkin Skin = PetSkin.DeepBlue;
        public bool Walking;
        public float WalkPhase;
        public int Facing = 1;

        public static PetSkin ParseSkin(string s)
        {
            if (s == "sakura") return PetSkin.Sakura;
            if (s == "mint") return PetSkin.Mint;
            return PetSkin.DeepBlue;
        }
    }
}
