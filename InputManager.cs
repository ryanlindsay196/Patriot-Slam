using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using YUIS;
using DiscGame.Gameplay.UI;
using DiscGame.Dialog;
using System;

namespace DiscGame.Gameplay
{
    public class InputManager : MonoBehaviour
    {
        //[SerializeField]
        //GameObject TEMP_VisualIndicator;

        [SerializeField]
        float movePositionRefreshRate;
        float movePositionRefreshTimer;

        [SerializeField]
        float lobChance;
        //[SerializeField]
        //float awareness;//How fast the CPU notices things
        [SerializeField]
        float frisbeePredictionErrorRange;
        enum ActionTypes { dash, throwFrisbee, lobFrisbee, move }
        class ActionData
        {
            public ActionTypes action;
            public Vector2 position;

            public void SetActionType(ActionTypes in_Action)
            {
                action = in_Action;
            }
            public void SetPosition(Vector2 in_Position)
            {
                position = in_Position;
                //Debug.Log("CPUMovement::ActionData::SetPosition(Vector2)::Position = " + in_Position);
            }
        }
        //List<ActionTypes> enemyActions;
        List<ActionData> actions;

        PlayerMovement pmov;

        bool decidedThrow = false;

        [SerializeField]
        public bool isCPU;
        public void MakeCPU()
        {
            isCPU = true;
        }

        private void Start()
        {
            actions = new List<ActionData>();
            pmov = GetComponent<PlayerMovement>();
        }

        private void Update()
        {
            if (isCPU)
            {
                if (actions.Count <= 0)
                {//if no action is loaded
                    if (pmov.frisbees[0].players[0].FindCurrentlyHeldFrisbeeIndex() == -1 && pmov.frisbees[0].players[1].FindCurrentlyHeldFrisbeeIndex() == -1)
                    {//if neither player is holding the frisbee
                     //if (actions[0].action != ActionTypes.dash || actions[0].action != ActionTypes.move)
                        {
                            CPUAddActionData(ActionTypes.move);
                            ResetMovePosition();
                        }
                    }
                    /*if (pmov.FindCurrentlyHeldFrisbeeIndex() != -1)
                    {//if CPU is holding frisbee
                        if (UnityEngine.Random.Range(0, 100) <= lobChance)
                            CPUAddActionData(ActionTypes.lobFrisbee);
                        else
                            CPUAddActionData(ActionTypes.throwFrisbee);
                    }*/
                    if (actions.Count > 0)
                    {
                        if (actions[0].action == ActionTypes.move || actions[0].action == ActionTypes.dash)
                        {
                            movePositionRefreshTimer -= Time.deltaTime;
                            if (movePositionRefreshTimer < 0)
                            {
                                actions[0].position = new Vector2();
                                //Debug.Log("InputManager::Update()::Refresh move position timer");
                                ResetMovePosition();
                            }
                            if ((actions[0].position - new Vector2(transform.position.x, transform.position.y)).magnitude >= 2f)
                            {
                                actions[0].SetActionType(ActionTypes.dash);
                            }
                        }
                    }
                }
                else if (pmov.FindCurrentlyHeldFrisbeeIndex() != -1)
                {
                    if (actions[0].action == ActionTypes.dash || actions[0].action == ActionTypes.move)
                    {
                        //actions[0].position = new Vector2(UnityEngine.Random.Range(4, 14) * pmov.playerSide, UnityEngine.Random.Range(-4, 4));
                    }
                }
                if (pmov.FindCurrentlyHeldFrisbeeIndex() != -1)
                {
                    if (!decidedThrow)
                    {
                        if (UnityEngine.Random.value <= .25f)
                        {//randomly perform a throw that isn't a special
                            var throwData = pmov.SpecialMoves[UnityEngine.Random.Range(0, pmov.SpecialMoves.Length - 2)];
                            pmov.BufferFrisbeeThrow(throwData, GetDumbAxis(pmov.playerID, "Move"));

                            decidedThrow = true;
                        }
                        else if (pmov.IsPerformingSuper && GetComponent<NPC>().dialogue.name != "Kels")
                        {//perform a special
                            pmov.PerformSuper(Vector2.zero);
                            decidedThrow = true;
                        }
                        else if (UnityEngine.Random.value <= 0.2f)
                        {//perform a normal throw
                            pmov.BufferFrisbeeThrow(null, new Vector2(UnityEngine.Random.Range(-1, 1), UnityEngine.Random.Range(-1, 1)));
                            decidedThrow = true;
                        }
                        else if (UnityEngine.Random.value <= 0.2f)
                        {
                            pmov.BufferFrisbeeLob(new Vector2(UnityEngine.Random.Range(-1, 1), UnityEngine.Random.Range(-1, 1)));
                            decidedThrow = true;
                        }
                    }
                }
                else
                {
                    if (GetComponent<NPC>().dialogue.name == "Kels")
                    {//Kels's Super
                        if (UnityEngine.Random.value <= 0.08f && Vector3.Distance(transform.position,pmov.frisbees[0].transform.position) > 17 && pmov.canPerformSuper)
                        {
                            pmov.KelsSpecial();// PerformSuper(GetDumbAxis(pmov.playerID, "Move"));
                        }
                    }
                    decidedThrow = false;
                }
            }
        }
        public Vector2 ResetMovePosition()
        {
            //Debug.Log("InputManager::ResetMovePosition()::Resetting move position");
            if (actions[0].action == ActionTypes.move || actions[0].action == ActionTypes.dash)
            {
                if (actions[0].position == new Vector2())
                {//set movement to try and intercept the frisbee
                    RaycastHit hit;
                    Ray frisbeeRay = new Ray(pmov.frisbees[0].transform.position, pmov.frisbees[0].GetComponent<Rigidbody>().velocity);

                    if (pmov.frisbees[0].GetComponent<Rigidbody>().velocity.z != 0)
                    {//if frisbee moving diagonally
                        //Debug.Log("InputManager::ResetMovePosition()::Frisbee moving diagonally");
                        //Vector3 reverseFrisbeeVelocity = new Vector3(frisbees[0].GetComponent<Rigidbody>().velocity.x, 0, -frisbees[0].GetComponent<Rigidbody>().velocity.z);
                        if (Physics.Raycast(frisbeeRay, out hit, Mathf.Infinity))
                        {
                            //frisbeeRay = new Ray(hit.transform.position, reverseFrisbeeVelocity);
                            //Physics.Raycast(frisbeeRay, out hit, UnityEngine.Random.Range(2, 4));
                            //if (Mathf.Sign(hit.point.x) != pmov.playerSide)
                            //    hit.point = new Vector3(-hit.point.x, hit.point.y, hit.point.z);
                            actions[0].SetPosition(new Vector2(hit.point.x, -hit.point.z) + new Vector2(UnityEngine.Random.Range(-frisbeePredictionErrorRange, frisbeePredictionErrorRange), UnityEngine.Random.Range(-frisbeePredictionErrorRange, frisbeePredictionErrorRange)));
                            //Debug.Log("InputManager::ResetMovePosition()::predicted frisbee location " + actions[0].position);
                        }
                    }
                    else
                    {
                        //actions[0].SetPosition(new Vector2(pmov.frisbees[0].transform.position.x * 0, -pmov.frisbees[0].transform.position.z) + new Vector2(UnityEngine.Random.Range(4, 14) * pmov.playerSide, pmov.frisbees[0].transform.position.z));
                        actions[0].SetPosition(new Vector2(0, -pmov.frisbees[0].transform.position.z) + new Vector2(UnityEngine.Random.Range(4, 14) * pmov.playerSide, 0));
                    }
                }
                if ((new Vector2(transform.position.x, transform.position.z) - actions[0].position).magnitude <= 2f)
                {//if close enough to desired position
                    //Debug.Log("CPUMovement::GetDumbAxis(int,string)::Removed action at index 0");
                    actions.RemoveAt(0);
                    return new Vector2();
                }
                if (actions.Count > 0)
                {
                    if (Mathf.Sign(actions[0].position.x) == pmov.playerSide)
                    {//make sure the selected position is on this CPU's side of the field
                        actions[0].SetPosition(new Vector2(actions[0].position.x * -1, actions[0].position.y));
                    }
                    if (actions[0].position.x == 0)
                    {//if the selected X position is 0, move towards the CPU's goal
                        actions[0].SetPosition(new Vector2(5 * pmov.playerSide, actions[0].position.y));
                    }
                    if (Mathf.Abs(actions[0].position.x) > 14)
                    {//make sure that the cpu can't select a point outside of the arena
                        //Debug.Log("CPUMovement::GetDumbAxis(int,string)::Moved target position back inside the arena");
                        actions[0].SetPosition(new Vector2(Mathf.Sign(actions[0].position.x) * 14, actions[0].position.y));
                    }
                    //if(actions[0].position.y > pmov.frisbees[0].transform.position.z)
                    {
                        //actions[0].SetPosition(actions[0].position + new Vector2(0, -0.3f * (pmov.frisbees[0].transform.position.y - transform.position.z)));
                    }

                    //if lobbing
                    if(pmov.frisbees[0].GetComponent<FrisbeePhysics>().isLobbingToX != -1)
                    {
                        Invoke("LookForLob", UnityEngine.Random.Range(0.3f, 1.2f));
                    }
                }
            }
            movePositionRefreshTimer = movePositionRefreshRate;
            //Invoke("ResetMovePosition", movePositionRefreshRate);
            //TEMP_VisualIndicator.transform.position = actions[0].position;
            //Debug.Log("New target move position: " + actions[0].position);
            return (actions[0].position - new Vector2(transform.position.x, -transform.position.z)).normalized;
        }

        private void LookForLob()
        {
            actions[0].SetPosition(new Vector2(pmov.frisbees[0].lobIndicator.transform.position.x, -pmov.frisbees[0].lobIndicator.transform.position.z) + new Vector2(UnityEngine.Random.Range(-2, 2), UnityEngine.Random.Range(-2, 2)));
        }

        void CPUAddActionData(ActionTypes actionType)
        {
            //Debug.Log("CPUMovement::AddActionData(ActionTypes)::Added " + actionType.ToString());
            actions.Add(new ActionData());
            actions[actions.Count - 1].SetActionType(actionType);
        }

        public virtual ButtonState GetDumbButton(int playerID, string input)
        {
            if (isCPU)
                return CPUGetDumbButton(playerID, input);
            if (playerID == 1 && KeyboardControlProfileOverrider.isUsingCombinedControls())
            {
                return InputCon.Instance.GetButton(0, input + "_2");
            }
            return InputCon.Instance.GetButton(playerID, input);
        }
        public virtual Vector2 GetDumbAxis(int playerID, string input)
        {
            if (isCPU)
                return CPUGetDumbAxis(playerID, input);
            if (playerID == 1 && KeyboardControlProfileOverrider.isUsingCombinedControls())
            {
                Vector2 v = InputCon.Instance.GetAxis(0, input + "_2");
                v.x *= -1;
                return v;
            }
            return InputCon.Instance.GetAxis(playerID, input);
        }
        public virtual bool GetDumbSequence(int playerID, string set, string name)
        {
            if (playerID == 1 && KeyboardControlProfileOverrider.isUsingCombinedControls())
            {
                return SequenceCon.Instance.GetSequence(0, set + "_2", name + "_2");
            }
            return SequenceCon.Instance.GetSequence(playerID, set, name);
        }



        protected virtual Vector2 CPUGetDumbAxis(int playerID, string input)
        {
            if (actions.Count > 0)
            {
                switch (input)
                {
                    case "Move":
                        //InvokeRepeating("ResetMovePosition", UnityEngine.Random.Range(reactionSpeedMin, reactionSpeedMax), movePositionRefreshRate);
                        //TEMP_VisualIndicator.transform.position = actions[0].position;
                        if (movePositionRefreshTimer <= 0)
                            return ResetMovePosition();
                        if ((actions[0].position - new Vector2(transform.position.x, -transform.position.z)).magnitude < 1f)
                            return new Vector2();
                        return -(actions[0].position - new Vector2(transform.position.x, -transform.position.z)).normalized;
                        break;
                }
            }
            return new Vector2();
        }

        public void CPUResetActions()
        {
           // Debug.Log("InputManager::ResetActions()");
            actions = new List<ActionData>();
        }

        ButtonState CPUGetDumbButton(int playerID, string input)
        {
            if (actions.Count > 0)
            {
                switch (input)
                {
                    case "Dash":
                        if (actions[0].action == ActionTypes.dash)
                        {
                            actions[0].SetActionType(ActionTypes.move);
                            return ButtonState.PRESSED;
                        }
                        break;
                    case "Lob":

                        if (actions[0].action == ActionTypes.lobFrisbee)
                        {
                            return ButtonState.PRESSED;
                        }
                        break;
                    case "Throw":
                        if (actions[0].action == ActionTypes.throwFrisbee)
                        {
                            return ButtonState.PRESSED;
                        }
                        break;
                }
            }
            return ButtonState.OFF;
        }
    }
}
