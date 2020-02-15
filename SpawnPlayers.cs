using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DiscGame.Gameplay.UI;
using DiscGame.Menus;
namespace DiscGame.Gameplay
{
    public class SpawnPlayers : MonoBehaviour
    {
        [SerializeField]
        GameObject[] playerPrefabs;
        [SerializeField]
        GameObject spawnPoint1, spawnPoint2;

        public GameObject player1, player2;
        public bool spawnPlayers = true;

        // Start is called before the first frame update
        void Awake()
        {
            if (spawnPlayers)
            {
                //CharacterSelectP2.character1isClick = true;
                //CharacterSelectP2.character3isClickP2 = true;

                if (CharacterSelect1.playerChoices[0].characterPrefabURL == "pMaxwell")
                {
                    player1 = Instantiate(playerPrefabs[0], spawnPoint1.transform.position, transform.rotation);
                }
                else if (CharacterSelect1.playerChoices[0].characterPrefabURL == "pCHAD")
                {
                    player1 = Instantiate(playerPrefabs[1], spawnPoint1.transform.position, transform.rotation);
                }
                else if (CharacterSelect1.playerChoices[0].characterPrefabURL == "pAriela")
                {
                    player1 = Instantiate(playerPrefabs[2], spawnPoint1.transform.position, transform.rotation);
                }
                else if (CharacterSelect1.playerChoices[0].characterPrefabURL == "pKels")
                {
                    player1 = Instantiate(playerPrefabs[3], spawnPoint1.transform.position, transform.rotation);
                }
                else if (CharacterSelect1.playerChoices[0].characterPrefabURL == "pSHLOPP")
                {
                    player1 = Instantiate(playerPrefabs[4], spawnPoint1.transform.position, transform.rotation);
                }



                Vector3 p2RotationVector = new Vector3(0, 180, 0);//the rotation player 2 starts with
                if (CharacterSelect1.playerChoices[1].characterPrefabURL == "pMaxwell")
                {
                    player2 = Instantiate(playerPrefabs[0], spawnPoint2.transform.position, Quaternion.Euler(p2RotationVector));
                    if (CharacterSelect1.playerChoices[1].isCPU)
                    { player2.GetComponent<InputManager>().MakeCPU(); }
                }
                else if (CharacterSelect1.playerChoices[1].characterPrefabURL == "pCHAD")
                {
                    player2 = Instantiate(playerPrefabs[1], spawnPoint2.transform.position, Quaternion.Euler(p2RotationVector));
                    if (CharacterSelect1.playerChoices[1].isCPU)
                    { player2.GetComponent<InputManager>().MakeCPU(); }
                }
                else if (CharacterSelect1.playerChoices[1].characterPrefabURL == "pAriela")
                {
                    player2 = Instantiate(playerPrefabs[2], spawnPoint2.transform.position, Quaternion.Euler(p2RotationVector));
                    if (CharacterSelect1.playerChoices[1].isCPU)
                    { player2.GetComponent<InputManager>().MakeCPU(); }
                }
                else if (CharacterSelect1.playerChoices[1].characterPrefabURL == "pKels")
                {
                    player2 = Instantiate(playerPrefabs[3], spawnPoint2.transform.position, Quaternion.Euler(p2RotationVector));
                    if (CharacterSelect1.playerChoices[1].isCPU)
                    { player2.GetComponent<InputManager>().MakeCPU(); }
                }
                else if (CharacterSelect1.playerChoices[1].characterPrefabURL == "pSHLOPP")
                {
                    player2 = Instantiate(playerPrefabs[4], spawnPoint2.transform.position, Quaternion.Euler(p2RotationVector));
                    if (CharacterSelect1.playerChoices[1].isCPU)
                    { player2.GetComponent<InputManager>().MakeCPU(); }
                }




                /*
                if (CharacterSelect.SinglePlayercharacter1isClick || CharacterSelectP2.character1isClick)
                {
                    player1 = Instantiate(playerPrefabs[0], spawnPoint1.transform.position, transform.rotation);
                }
                else if (CharacterSelect.SinglePlayercharacter2isClick || CharacterSelectP2.character2isClick)
                {
                    player1 = Instantiate(playerPrefabs[1], spawnPoint1.transform.position, transform.rotation);
                }
                else if (CharacterSelect.SinglePlayercharacter3isClick || CharacterSelectP2.character3isClick)
                {
                    player1 = Instantiate(playerPrefabs[2], spawnPoint1.transform.position, transform.rotation);
                }
                else if (CharacterSelect.SinglePlayercharacter4isClick || CharacterSelectP2.character4isClick)
                {
                    player1 = Instantiate(playerPrefabs[3], spawnPoint1.transform.position, transform.rotation);
                }
                

                Vector3 p2RotationVector = new Vector3(0, 180, 0);//the rotation player 2 starts with
                if (CharacterSelect.character1isClickCPU || CharacterSelectP2.character1isClickP2)
                {
                    player2 = Instantiate(playerPrefabs[0], spawnPoint2.transform.position, Quaternion.Euler(p2RotationVector));
                }
                else if (CharacterSelect.character2isClickCPU || CharacterSelectP2.character2isClickP2)
                {
                    player2 = Instantiate(playerPrefabs[1], spawnPoint2.transform.position, Quaternion.Euler(p2RotationVector));
                }
                else if (CharacterSelect.character3isClickCPU || CharacterSelectP2.character3isClickP2)
                {
                    player2 = Instantiate(playerPrefabs[2], spawnPoint2.transform.position, Quaternion.Euler(p2RotationVector));
                }
                else if (CharacterSelect.character4isClickCPU || CharacterSelectP2.character4isClickP2)
                {
                    player2 = Instantiate(playerPrefabs[3], spawnPoint2.transform.position, Quaternion.Euler(p2RotationVector));
                }
                */


                GameObject.FindObjectOfType<Score>().player1 = player1;
                GameObject.FindObjectOfType<Score>().player2 = player2;

                GameObject.FindObjectOfType<Score>().P1RB = player1.GetComponent<Rigidbody>();
                GameObject.FindObjectOfType<Score>().P2RB = player2.GetComponent<Rigidbody>();

                player1.GetComponent<PlayerMovement>().playerID = 0;
                player1.GetComponent<PlayerMovement>().playerSide = 1;
                player2.GetComponent<PlayerMovement>().playerID = 1;
                player2.GetComponent<PlayerMovement>().playerSide = -1;
                player1.transform.position = GameObject.Find("Player1SpawnPosition").transform.position;
                player2.transform.position = GameObject.Find("Player2SpawnPosition").transform.position;
            }
        }
    }
}
