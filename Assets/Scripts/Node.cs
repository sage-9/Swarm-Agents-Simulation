public struct Node
{
    public NodeState NodeState;

    public Node(NodeState nodeState)
    {
        NodeState = nodeState;
    }
}

public enum NodeState
{
    Unknown,
    Unexplored,
    Occupied,
    Free
}
