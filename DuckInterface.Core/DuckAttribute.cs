using System;

[AttributeUsage(AttributeTargets.Class)]
public class DuckAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Interface)]
public class DuckableAttribute : Attribute { }
