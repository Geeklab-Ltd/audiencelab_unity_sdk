namespace Geeklab.AudiencelabSDK
{
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class FieldGroup : System.Attribute
    {
        public string GroupName;

        public FieldGroup(string groupName)
        {
            this.GroupName = groupName;
        }
    }
}