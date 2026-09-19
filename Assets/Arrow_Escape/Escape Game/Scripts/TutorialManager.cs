using UnityEngine;

namespace MultiTechStudio.EscapeGame
{
    public class TutorialManager : MonoBehaviour
    {
        [SerializeField] private GameObject tutorial1;
        [SerializeField] private GameObject tutorial6;

        public void ShowTutorial(int _levelIndex)
        {
            switch (_levelIndex)
            {
                case 0:
                    tutorial1.SetActive(true);
                    break;
                case var levelIndex when levelIndex == LevelManager.Instance.tutorialZoomLevel:
                    tutorial6.SetActive(true);
                    break;
            }
        }
        public void HideTutorial()
        {
            tutorial1.SetActive(false);
            tutorial6.SetActive(false);
        }
    }
}
