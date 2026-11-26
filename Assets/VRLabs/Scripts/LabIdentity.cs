using UnityEngine;

namespace VRLabs
{
    // Подпись хранится на сцене; её можно поменять в Inspector до запуска.
    [ExecuteAlways]
    public class LabIdentity : MonoBehaviour
    {
        public int laboratory = 1;
        public int variant = 1;
        public string studentName = "Артемьев Григорий";
        public string group = "ИУ6-71Б";
        public TextMesh board;

        void OnEnable() { UpdateBoard(); }
        void OnValidate() { UpdateBoard(); }
        void UpdateBoard()
        {
            if (board != null)
                board.text = "ЛАБОРАТОРНАЯ РАБОТА " + laboratory + "\n" +
                    studentName + "  ·  " + group + "\nВариант " + variant;
        }
    }
}
