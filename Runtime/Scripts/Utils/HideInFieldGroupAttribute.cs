using System;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class HideInFieldGroupAttribute : Attribute
{
    public string GroupName { get; private set; }

    public HideInFieldGroupAttribute(string groupName)
    {
        this.GroupName = groupName;
    }
}