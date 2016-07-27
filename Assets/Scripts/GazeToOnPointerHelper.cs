using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class GazeToOnPointerHelper : MonoBehaviour
{
    public List<IPointerEnterHandler> OnPointerEnterComponenets = new List<IPointerEnterHandler>();
    public List<IPointerExitHandler> OnPointerExitComponenets = new List<IPointerExitHandler>();
    public List<IPointerClickHandler> OnPointerClickComponenets = new List<IPointerClickHandler>();

    // Use this for initialization
    void Start()
    {
        foreach(Component comp in GetComponents<Component>())
        {
            if (typeof(IPointerClickHandler).IsAssignableFrom(comp.GetType()))
                OnPointerClickComponenets.Add(comp as IPointerClickHandler);
            if (typeof(IPointerExitHandler).IsAssignableFrom(comp.GetType()))
                OnPointerExitComponenets.Add(comp as IPointerExitHandler);
            if (typeof(IPointerEnterHandler).IsAssignableFrom(comp.GetType()))
                OnPointerEnterComponenets.Add(comp as IPointerEnterHandler);
        }
    }

    public void OnGazeEnter()
    {
        foreach(IPointerEnterHandler iEnter in OnPointerEnterComponenets)
        {
            iEnter.OnPointerEnter(new PointerEventData(EventSystem.current));
        }
    }

    public void OnGazeLeave()
    {
        foreach (IPointerExitHandler iExit in OnPointerExitComponenets)
        {
            iExit.OnPointerExit(new PointerEventData(EventSystem.current));
        }
    }

    public void OnSelect()
    {
        foreach (IPointerClickHandler iClick in OnPointerClickComponenets)
        {
            iClick.OnPointerClick(new PointerEventData(EventSystem.current));
        }
    }
}
