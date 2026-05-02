public class Node
{
    int X {get;set;}
    int Y {get;set;}
    int Z {get;set;}

    public NodeState NodeState;
}

public enum NodeState
{
    Unknown,
    Unexplored,
    Occupied,
    Free
}
