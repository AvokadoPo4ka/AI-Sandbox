using UnityEngine.EventSystems;
using UnityEngine.UI;

// Connection and cancellation controls require an intentional pointer click.
public sealed class PointerOnlyButton : Button {
    public override void OnSubmit(BaseEventData eventData) { }
}
