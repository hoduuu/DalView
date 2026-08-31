namespace DalView.ViewModels;

public static class SearchNavigator
{
    public static int Next(int currentIndex, int count)
    {
        if (count <= 0) return -1;
        return (currentIndex + 1) % count;
    }

    public static int Previous(int currentIndex, int count)
    {
        if (count <= 0) return -1;
        return currentIndex <= 0 ? count - 1 : currentIndex - 1;
    }
}
