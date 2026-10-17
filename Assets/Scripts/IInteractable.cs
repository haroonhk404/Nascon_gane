// Har interactable cheez (Candle, Door) ye interface implement karti hai
public interface IInteractable
{
    // Screen pe kya text dikhana hai (e.g. "[E] Grab Candle")
    string GetPrompt(PlayerInteractor player);

    // Jab player E press kare
    void Interact(PlayerInteractor player);
}
