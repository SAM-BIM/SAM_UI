namespace SAM.Core.UI
{
    public class Modification :IModification
    {
        // Undoable by default; transient modifications (e.g. camera-only view updates) override this.
        public virtual bool Undoable => true;
    }
}
