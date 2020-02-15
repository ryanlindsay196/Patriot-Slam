using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using YUIS;
using DiscGame.Gameplay.UI;
using DiscGame.Dialog;
using UnityEngine.Events;
using UnityEngine.UI;
using System;
namespace DiscGame.Gameplay
{
    public class PlayerMovement : MonoBehaviour
    {
        #region Identification
        /// <summary>
        /// The ID of the player, starting from 0. Most notably used for input.
        /// </summary>
        public int playerID;
        /// <summary>
        /// If on the left, set to 1, if on the right, set to -1.
        /// This reverses the player's axis inputs so macros feel more natural.
        /// </summary>
        public int playerSide = 1;
        #endregion

        #region Frisbee Related Parameters
        [SerializeField]
        float catchDistance;
        public float CatchDistance
        { get { return catchDistance; } }
        [SerializeField]
        float normalThrowSpeed;
        [SerializeField]
        float normalLobSpeed;
        [SerializeField]
        float angledThrowAngle = 45;
        //[SerializeField]//This is a list just in case the designers decide to add multiple frisbees
        public FrisbeePhysics[] frisbees;

        private bool doNormalThrow;
        private bool doNormalLob;

        [SerializeField]
        GameObject frisbeeCatchTransform;
        public GameObject FrisbeeCatchTransform
        { get { return frisbeeCatchTransform; } }

        public int noCatchWindow = 0;
        public int maxNoCatchWindow = 12;

        public bool canParry;
        #endregion
        #region Movement Related Parameters
        [SerializeField]
        float moveSpeed;
        [SerializeField]
        float moveAcceleration;

        [SerializeField]
        float dashSpeed;
        [SerializeField]
        float dashDeceleration;
        public Vector2 currentDSpd;
        public Vector2 currentKBSpd;
        #endregion

        #region Special Moves
        [Header("Special Move Info")]
        [SerializeField]
        string macroSetName;
        [SerializeField]
        ThrowDataContainer[] specialMoves;
        ThrowDataContainer normalThrowF;
        ThrowDataContainer normalThrowU;
        ThrowDataContainer normalThrowD;

        ThrowDataContainer bufferedThrow;
        Vector2 bufferedDirection;
        float bufferedPowerLevel = 1;
        bool hasABuffer = false;

        public bool canPerformSuper
        {
            get
            {
                return pUI.SpecialSliderValue >= 1;
            }
        }

        public ThrowDataContainer[] SpecialMoves
        {
            get { return specialMoves; }
        }

        public bool IsPerformingSuper
        {
            get { return isPerformingSuper; }
        }
        private bool isPerformingSuper
        {
            get
            {
                if (!GetComponent<InputManager>().isCPU)
                {
                    ButtonState dd = GetDumbButton(playerID, "Dash");
                    ButtonState tt = GetDumbButton(playerID, "Throw");
                    if (pUI.SpecialSliderValue < 1 || (tt != ButtonState.HELD || (dd != ButtonState.HELD && dd != ButtonState.PRESSED)))
                    { return false; }
                    return true;
                }
                else
                {
                    return canPerformSuper;
                    /*if(pUI.SpecialSliderValue >= 1)
                    {
                        Debug.Log("PlayerMovement::isPerformingSuper::Use Special");
                        return true;
                    }
                    return false;*/
                }
            }
        }
        #endregion

        #region References
        private Rigidbody rbSelf;
        private PlayerUI playerui;
        private PlayerUI pUI
        {
            get
            {
                playerui = playerui ?? GetComponentInChildren<PlayerUI>();
                return playerui;
            }
        }

        private DialogueManager dialogManager;

        public Animator aniSelf;

        private InputManager cpuPlayer;

        private SmearEffect smear;
        private SmearEffect Smear{get{if (smear == null) { smear = GetComponentInChildren<SmearEffect>(); }return smear;}}

        #endregion

        protected virtual void Start()
        {
            InputManager[] inputManagers = FindObjectsOfType<InputManager>();
            if(inputManagers[0].isCPU)
            {
                cpuPlayer = inputManagers[0];
            }
            else if(inputManagers[1].isCPU)
            {
                cpuPlayer = inputManagers[1];
            }

            cpuPlayer = FindObjectOfType<InputManager>();
            rbSelf = GetComponent<Rigidbody>();
            SetupThrows();
            //frisbeePhysicsData = new FrisbeePhysicsData();
            frisbees = GameObject.FindObjectsOfType<FrisbeePhysics>();
            /* [Jacob]
             * Calling InputCon.Instance.SetActivePlayerCount is important. 
             * If this playerID is 1 then there must be two players, so we need to make sure InputCon is looking for inputs for two players.
             */
            if (playerID + 1 > InputCon.Instance.GetActivePlayerCount() && !cpuPlayer.isCPU) { InputCon.Instance.SetActivePlayerCount(playerID + 1); }
            StartCoroutine(WaitToReverseInputs());
            dialogManager = FindObjectOfType<DialogueManager>();
            aniSelf = GetComponentInChildren<Animator>();
        }
        #region Startup
        /// <summary>
        /// Called on start, initializes normal throws and sets up specials.
        /// </summary>
        void SetupThrows()
        {
            #region Define normal throws
            normalThrowF = new ThrowDataContainer();
            normalThrowU = new ThrowDataContainer();
            normalThrowD = new ThrowDataContainer();

            Vector2 angU = (Quaternion.Euler(0, 0, angledThrowAngle) * Vector2.right).normalized;
            Vector2 angD = (Quaternion.Euler(0, 0, -angledThrowAngle) * Vector2.right).normalized;

            normalThrowF.macroName = "";
            normalThrowF.stopOnWallCollision = true;
            normalThrowF.curves.AddKey(new Keyframe(0, normalThrowSpeed * playerSide), new Keyframe(0, 0), new Keyframe(0,0));
            normalThrowF.curves.AddKey(new Keyframe(1, normalThrowSpeed * playerSide), new Keyframe(1, 0), new Keyframe(1, 0));

            normalThrowU.macroName = "";
            normalThrowU.stopOnWallCollision = true;
            normalThrowU.curves.AddKey(new Keyframe(0, angU.x * normalThrowSpeed * playerSide), new Keyframe(0, 0), new Keyframe(0, angU.y * normalThrowSpeed));
            normalThrowU.curves.AddKey(new Keyframe(1, angU.x * normalThrowSpeed * playerSide), new Keyframe(1, 0), new Keyframe(1, angU.y * normalThrowSpeed));

            normalThrowD.macroName = "";
            normalThrowD.stopOnWallCollision = true;
            normalThrowD.curves.AddKey(new Keyframe(0, angD.x * normalThrowSpeed * playerSide), new Keyframe(0, 0), new Keyframe(0, -angU.y * normalThrowSpeed));
            normalThrowD.curves.AddKey(new Keyframe(1, angD.x * normalThrowSpeed * playerSide), new Keyframe(1, 0), new Keyframe(1, -angU.y * normalThrowSpeed));
            #endregion
            #region Make all special throws side dependent

            for (int i = 0; i < specialMoves.Length; i++)
            {specialMoves[i] = SideDependentX(specialMoves[i]);}
            #endregion
            #region Make normal throws loop
            normalThrowF.curves.SetPreWrapMode(WrapMode.Loop, true, true, true);
            normalThrowF.curves.SetPostWrapMode(WrapMode.Loop, true, true, true);

            normalThrowU.curves.SetPreWrapMode(WrapMode.Loop, true, true, true);
            normalThrowU.curves.SetPostWrapMode(WrapMode.Loop, true, true, true);

            normalThrowD.curves.SetPreWrapMode(WrapMode.Loop, true, true, true);
            normalThrowD.curves.SetPostWrapMode(WrapMode.Loop, true, true, true);
            #endregion
        }
        /// <summary>
        /// Reverses X values of a ThrowDataContainer.
        /// </summary>
        /// <param name="smc">The ThrowDataContainer to reverse x values.</param>
        /// <returns>The x reversed ThrowDataContainer.</returns>
        ThrowDataContainer SideDependentX(ThrowDataContainer smc)
        {
            for (int i = 0; i < smc.curves.x.keys.Length;i++)
            {
                Keyframe kf = smc.curves.x.keys[i];
                kf.value *= playerSide;
                kf.outTangent *= playerSide;
                kf.inTangent *= playerSide;
                smc.curves.x.MoveKey(i, kf);
            }
            return smc;
        }
        /// <summary>
        /// Wait until this player is validated by the input system before reversing their controls by side.
        /// </summary>
		private IEnumerator WaitToReverseInputs()
        {
            //Wait until this player is identified 
            yield return new WaitUntil(() => (InputCon.Instance.AxesDirs.Count >= playerID + 1));
            InputCon.Instance.AxesDirs[playerID] = new Vector2Int(playerSide, 1);
        }
        #endregion
        //Use FixedUpdate when possible for framerate dependence.
        protected virtual void FixedUpdate()
        {
            noCatchWindow = noCatchWindow <= 0 ? 0 : noCatchWindow - 1;
            ThrowDataContainer _spec = GetFrisbeePhysicsData();

            canParry = (currentDSpd.magnitude <= 0 && GetDumbAxis(playerID, "Move").magnitude <= 0.05f && (GetDumbButton(playerID, "Throw") == ButtonState.HELD || GetDumbButton(playerID, "Throw") == ButtonState.PRESSED));

            #region movement code
            SetAniBool("isHolding", FindCurrentlyHeldFrisbeeIndex() != -1);
            if (currentDSpd.magnitude <= 0)
            { SetAniFloat("dashSpeedDelta", 2); }
            else { SetAniFloat("dashSpeedDelta", Mathf.InverseLerp(dashSpeed, 0, currentDSpd.magnitude)); }
            SetAniFloat("moveSpeedMag", Mathf.InverseLerp(0, moveSpeed, rbSelf.velocity.magnitude));
            if (frisbees.Length > 0)
            {
                SetAniBool("isUnderFrisbee", (frisbees[0].isLobbingToX != -1) && (Vector3.Distance(Vector3.Scale(new Vector3(1,0,1),transform.position), Vector3.Scale(new Vector3(1, 0, 1), frisbees[0].lobIndicator.transform.position)) <= 1.2f));
            }
            if (!aniSelf.GetBool("knockedDown"))
            {
                if (FindCurrentlyHeldFrisbeeIndex() == -1) //FindCurrentlyHeldFrisbeeIndex returns -1 if no frisbees are held
                {
                    SetAniTrigger("lobFrisbee", true);
                    SetAniTrigger("throwFrisbee", true);
                    SetAniTrigger("catchFrisbee", true);
                    SetAniTrigger("catchLobbedFrisbee", true);
                    bool _esc = false;
                    if (_spec != null)
                    {
                        if (_spec.noHoldReq)
                        {
                            ThrowFrisbee(_spec);
                            _spec = null;
                            _esc = true;
                        }
                    }
                    if (!_esc)
                    {

                        // You can't move or dash again until your current dash is finished.
                        if (Mathf.Abs(currentDSpd.magnitude) <= 0)
                        {
                            if (Smear != null)
                                Smear.SetSmearLength(1, Mathf.Lerp(Smear.smearMat.GetFloat("_NoiseHeight"), 0, Time.fixedDeltaTime * 10));
                            Vector2 _move = GetDumbAxis(playerID, "Move");
                            Vector3 _cVel = rbSelf.velocity;
                            //Remember to multiply xinput by playerSide so input acts as normal outside of macros.
                            Vector3 targetV = new Vector3(_move.x * moveSpeed * playerSide, _cVel.y, _move.y * moveSpeed);
                            rbSelf.velocity = Vector3.Lerp(_cVel, targetV, moveAcceleration);

                            #region Dash code

                            if (GetDumbButton(playerID, "Dash") == ButtonState.PRESSED)
                            { Dash(_move); }
                        }
                        else
                        {
                            if (Smear != null)
                                Smear.SetSmearLength(15, 30);
                            rbSelf.velocity = new Vector3(currentDSpd.x, rbSelf.velocity.y, currentDSpd.y);
                            currentDSpd = Vector2.Lerp(currentDSpd, Vector3.zero, dashDeceleration);
                            if (Mathf.Abs(currentDSpd.magnitude) < moveSpeed)
                            { currentDSpd = Vector2.zero; }
                        }
                        #endregion
                        if (aniSelf != null)
                        {
                            if (rbSelf.velocity.magnitude > .01f)
                            { aniSelf.gameObject.transform.forward = rbSelf.velocity.normalized; }
                            else
                            { aniSelf.gameObject.transform.forward = transform.right; }
                        }
                    }

                }
                #endregion
                else //if holding a frisbee
                {
                    currentDSpd = Vector2.zero;
                    rbSelf.velocity = Vector3.zero;
                    if (!hasABuffer)
                    {
                        if (aniSelf != null)
                        {
                            aniSelf.gameObject.transform.forward = transform.right;
                            float _y = GetDumbAxis(playerID, "Move").y;
                            if (Mathf.Abs(_y) > .8f) { aniSelf.gameObject.transform.localEulerAngles = aniSelf.gameObject.transform.localEulerAngles + Vector3.up * angledThrowAngle * Mathf.Sign(_y) * -playerSide; }
                            else if (Mathf.Abs(_y) > .3f) { aniSelf.gameObject.transform.localEulerAngles = aniSelf.gameObject.transform.localEulerAngles + Vector3.up * angledThrowAngle * Mathf.Sign(_y) * .5f * -playerSide; }
                        }

                        if (_spec != null)
                        {
                            if (!_spec.noHoldReq)
                            {
                                BufferFrisbeeThrow(_spec, GetDumbAxis(playerID, "Move"));
                                _spec = null;
                            }
                        }
                        else if (doNormalLob)
                        { BufferFrisbeeLob(GetDumbAxis(playerID, "Move")); }
                        else if (GetDumbButton(playerID, "Lob") == ButtonState.PRESSED)
                        { doNormalLob = true; }
                        else if (doNormalThrow && !isPerformingSuper)
                        { BufferFrisbeeThrow(null, GetDumbAxis(playerID, "Move")); }
                        else if (GetDumbButton(playerID, "Throw") == ButtonState.PRESSED)
                        { doNormalThrow = true; }//frisbee throw sound

                        if (GetComponent<PlayerUI>().PowerSliderValue <= 0)
                            BufferFrisbeeThrow(null, GetDumbAxis(playerID, "Move"));
                    }
                }
            }
            else
            {
                rbSelf.velocity = Vector3.zero;
                currentDSpd = Vector3.zero;
            }
            if (currentKBSpd != Vector2.zero)
            {
                currentKBSpd = currentKBSpd.normalized * Mathf.Min(1.2f, currentKBSpd.magnitude);
                transform.position += new Vector3(currentKBSpd.x, 0, currentKBSpd.y);
                currentKBSpd = Vector2.Lerp(currentKBSpd, Vector2.zero, Time.fixedDeltaTime *20);
            }
        }
        #region Input
        protected virtual ButtonState GetDumbButton(int playerID, string input)
        {return GetComponent<InputManager>().GetDumbButton(playerID, input);}
        protected virtual Vector2 GetDumbAxis(int playerID, string input)
        {return GetComponent<InputManager>().GetDumbAxis(playerID, input);}
        protected virtual bool GetDumbSequence(int playerID,string set, string name)
        {return GetComponent<InputManager>().GetDumbSequence(playerID, set, name);}
        #endregion
        protected void Dash(Vector2 _move)
		{
            if (_move != Vector2.zero)
            {
                rbSelf.velocity = Vector3.zero;
                Vector3 ds = dashSpeed * ((Quaternion.Euler(0, 0, InputHelper.VectorToAngle(_move, 45)) * Vector2.right));
                ds.y = Mathf.Abs(ds.y) * Mathf.Sign(_move.y);
                ds.x = Mathf.Abs(ds.x) * Mathf.Sign(_move.x);
                currentDSpd = new Vector2(ds.x*playerSide, ds.y);
                //if (_move.y < 0) { currentDSpd.y *= -1; }
            }
        }
        #region Animator
        public void SetAniBool(string name, bool val)
        {
            if (aniSelf != null)
            {aniSelf.SetBool(name, val);}
        }
        public void SetAniTrigger(string name,bool reset = false)
        {
            if (aniSelf != null)
            {
                if (!reset)
                { aniSelf.SetTrigger(name); }
                else { aniSelf.ResetTrigger(name); }
            }
        }
        public void SetAniInt(string name, int val)
        {
            if (aniSelf != null)
            { aniSelf.SetInteger(name, val); }
        }
        public void SetAniFloat(string name, float val)
        {
            if (aniSelf != null)
            { aniSelf.SetFloat(name, val); }
        }
        #endregion
        protected ThrowDataContainer GetFrisbeePhysicsData()
        {
            for (int i = 0; i < specialMoves.Length; i++)
            {
                if (GetDumbSequence(playerID, macroSetName, specialMoves[i].macroName))
                {
                    //Debug.Log(specialMoves[i].macroName);
                    return specialMoves[i];
                }//We can break here because there's already logic in place to where it's very very unlikely that multiple macros are activated at the same time.//
            }
            return null;
        }

        public void PerformSuper(Vector2 in_DumbAxis)
        {
            for (int i = 0; i < specialMoves.Length; i++)
            {
                if (specialMoves[i].macroName == "Super")
                {
                    BufferFrisbeeThrow(specialMoves[i], in_DumbAxis);
                }
            }
        }
        public void BufferFrisbeeThrow(ThrowDataContainer pd, Vector2 dir)
        {
            if (!GameManager.bHasWon)
            {
                SetAniBool("isHolding", true);
                if (pd != null)
                {
                    if (pUI.SpecialSliderValue >= pd.meterUsage)
                    {
                        //Debug.Log("Successfully activated super move");
                        pUI.SpecialSliderValue -= pd.meterUsage;
                        SetAniInt("specialThrowID", pd.ID);
                        if (dialogManager != null && pd.macroName == "Super")
                        { dialogManager.ClearQueue(); dialogManager.players[playerID].GetComponent<NPC>().TriggerSpecialSentence(); }//Trigger for special dialogue *Doesnt break anything I promise* - Zach
                    }
                    else
                    {
                        //Debug.Log("Failed to activate super move");
                        return;
                    }
                }
                else
                {
                    SetAniTrigger("throwFrisbee");
                }
                bufferedThrow = pd;
                bufferedDirection = dir;
                hasABuffer = true;
                bufferedPowerLevel = pUI.GetPowerLevel();
            }
        }
        public void BufferFrisbeeLob(Vector2 input)
        {
            SetAniBool("isHolding", true);
            SetAniTrigger("lobFrisbee");
            bufferedDirection = input;
            bufferedPowerLevel = pUI.GetPowerLevel();
            hasABuffer = true;
        }
        public void ThrowBufferedFrisbee()
        {
            hasABuffer = false;
            ThrowDataContainer pd = bufferedThrow;
            float _y = bufferedDirection.y;
            //bufferedThrow = null;
            bufferedDirection = Vector2.zero;
            if (!GameManager.bHasWon)
            {
                bool _ignorecurves = false;
                if (pd != null) { _ignorecurves = pd.noHoldReq; }
                if (!_ignorecurves)
                {
                    if (cpuPlayer != null)
                        cpuPlayer.CPUResetActions();
                    //Debug.Log("PLAYER THROW");
                    doNormalThrow = false;
                    bool sp = true;
                    if (pd == null)
                    {
                        pd = normalThrowF;
                        sp = false;
                        Vector3 _norm = Vector3.right;
                        if (_y > .8f) { pd = normalThrowU; }
                        else if (_y > .3f) { pd = normalThrowU; pd.ScaleSpeed(new Vector3(1, 1, .5f)); }
                        if (_y < -.8f) { pd = normalThrowD; }
                        else if (_y < -.3f) { pd = normalThrowD; pd.ScaleSpeed(new Vector3(1, 1, .5f)); }
                    }
                    int ind = FindCurrentlyHeldFrisbeeIndex();
                    if (pd.noHoldReq) { ind = 0; }
                    if (ind != -1)
                    {
                        if (frisbees[ind].GetComponent<FrisbeeWooshSFX>() != null)
                        { frisbees[ind].GetComponent<FrisbeeWooshSFX>().PlayWoosh(bufferedPowerLevel); }
                        frisbees[ind].ThrowFrisbee(pd, sp,bufferedPowerLevel);
                    }
                }
                else
                {
                    int ind = 0;
                    frisbees[ind].ResetOwner();
                    pd.onOverride.Invoke();
                }
            }
        }
        public void LobBufferedFrisbee()
        {
            hasABuffer = false;
            Vector2 input = bufferedDirection;
            bufferedDirection = Vector2.zero;
            doNormalLob = false;
            if (cpuPlayer != null) { cpuPlayer.CPUResetActions(); }
            Vector3 ds = normalLobSpeed * ((Quaternion.Euler(0, 0, InputHelper.VectorToAngle(new Vector2(1, input.y), 45)) * Vector2.right));
            ds.z = -ds.y;
            ds.y = 0;
            int pos = 0;
            if (playerID == 1) { pos = 3; }
            if (input.x < -.3f) { ds.x *= .33f; }
            else if (input.x < .3f) { pos += 1; ds.x *= .66f; }
            else { pos += 2; }
            int ind = FindCurrentlyHeldFrisbeeIndex();
            if (ind != -1)
            {
                ds.x = (normalLobSpeed) * playerSide;
                if (input.x < -.3f) { ds.x *= .33f; }
                else if (input.x < .3f) { ds.x *= .66f; }
                ds.z = input.y;
               // Debug.Log("LOB WITH Z = " + ds.z);
               // Debug.Log(ds);
                frisbees[ind].LobFrisbee(ds, pos, bufferedPowerLevel);
            }
        }
        #region DEPRECATED
        protected void ThrowFrisbee(ThrowDataContainer pd)
        {
            //ThrowDataContainer pd = bufferedThrow;
            //float _y = bufferedDirection.y;
            if (!GameManager.bHasWon)
            {
                SetAniBool("isHolding", true);
                if (pd != null)
                {
                  //  Debug.Log("METER: " + pUI.SpecialSliderValue + " METER USAGE: " + pd.meterUsage);
                    if (pUI.SpecialSliderValue >= pd.meterUsage)
                    {
                        pUI.SpecialSliderValue -= pd.meterUsage;
                        SetAniInt("specialThrowID", pd.ID);
                        if (dialogManager != null)
                        { dialogManager.ClearQueue(); dialogManager.players[playerID].GetComponent<NPC>().TriggerSpecialSentence(); }//Trigger for special dialogue *Doesnt break anything I promise* - Zach
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    SetAniTrigger("throwFrisbee");
                }
                bool _ignorecurves = false;
                if (pd != null) { _ignorecurves = pd.noHoldReq; }
                if (!_ignorecurves)
                {
                    if (cpuPlayer != null)
                        cpuPlayer.CPUResetActions();
                    //Debug.Log("PLAYER THROW");
                    doNormalThrow = false;
                    bool sp = true;
                    if (pd == null)
                    {
                        pd = normalThrowF;
                        sp = false;
                        Vector3 _norm = Vector3.right;
                        float _y = GetDumbAxis(playerID, "Move").y;
                        if (_y > .8f) { pd = normalThrowU; }
                        else if (_y > .3f) { pd = normalThrowU; pd.ScaleSpeed(new Vector3(1, 1, .5f)); }
                        if (_y < -.8f) { pd = normalThrowD; }
                        else if (_y < -.3f) { pd = normalThrowD; pd.ScaleSpeed(new Vector3(1, 1, .5f)); }
                       // Debug.Log("ANGLE");
                    }
                    int ind = FindCurrentlyHeldFrisbeeIndex();
                    if (pd.noHoldReq) { ind = 0; }
                    if (ind != -1)
                    {
                        if (frisbees[ind].GetComponent<FrisbeeWooshSFX>() != null)
                        { frisbees[ind].GetComponent<FrisbeeWooshSFX>().PlayWoosh(pUI.GetPowerLevel()); }
                        frisbees[ind].ThrowFrisbee(pd, sp, pUI.GetPowerLevel());
                    }
                }
                else
                {
                    int ind = 0;// FindCurrentlyHeldFrisbeeIndex();
                    frisbees[ind].ResetOwner();
                    pd.onOverride.Invoke();
                }
            }
        }
        protected void LobFrisbee()
        {
            SetAniBool("isHolding", true);
            Vector2 input = GetDumbAxis(playerID, "Move");
            doNormalLob = false;
            if (cpuPlayer != null) { cpuPlayer.CPUResetActions(); }
            Vector3 ds = normalLobSpeed * ((Quaternion.Euler(0, 0, InputHelper.VectorToAngle(new Vector2(1,input.y), 45)) * Vector2.right));
            ds.z = -ds.y;
            ds.y = 0;
            int pos = 0;
            if (playerID == 1) { pos = 3; }
            if (input.x < -.3f) { ds.x *= .33f; }
            else if (input.x < .3f) { pos += 1; ds.x *= .66f; }
            else { pos += 2; }
          //  Debug.Log("POS: " + pos);
            int ind = FindCurrentlyHeldFrisbeeIndex();
            if (ind != -1)
            {
                ds.x = (normalLobSpeed) * playerSide;
                if (input.x < -.3f) { ds.x *= .33f; }
                else if (input.x < .3f) { ds.x *= .66f; }
                ds.z = input.y;
             //   Debug.Log(ds);
                frisbees[ind].LobFrisbee(ds, pos, pUI.GetPowerLevel());
                SetAniTrigger("lobFrisbee");
            }
        }
        #endregion
        protected ThrowDataContainer FactorThrowSpeed(ThrowDataContainer th)
        {
            if (pUI != null)
            {
                Vector3 spd = Vector3.one * pUI.GetPowerLevel();
                
                th.ScaleSpeed(spd);
            }
            return th;
        }
        public int FindCurrentlyHeldFrisbeeIndex()
        {
            for (int i = 0; i < frisbees.Length; i++)
            {
                if (frisbees[i].GetComponent<FrisbeePhysics>().catchTransform == frisbeeCatchTransform)
                {return i;}
            }
            return -1;
        }
        public ThrowDataContainer[] LoadThrows()
        {return specialMoves;}
        public void SaveThrows(ThrowDataContainer[] smovs)
        {specialMoves = smovs;}

        //Stop dashing if you hit a wall//
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.tag == "Wall")
            { currentDSpd = Vector3.zero;}
        }

        public void KelsSpecial()
        {
            Vector3 vel = frisbees[0].GetVelocity();
            pUI.SpecialSliderValue = 0;
            frisbees[0].GetComponent<Score>().SendToPlayer(playerID,false);
            frisbees[0].inKelsSpecial = true;
            frisbees[0].SetVelocity(frisbees[0].GetVelocity().normalized*vel.magnitude*2);
        }
    }
    [System.Serializable]
    public class ThrowDataContainer //: ScriptableObject
    {
        public int ID;
        public string macroName;
        public FrisbeeCurve curves;
        public bool stopOnWallCollision;
        public bool overrideSpeedCap;
        public bool forceKnockdown;
        public float meterUsage;
        public bool noHoldReq;
        public bool overrideCurves;
        public UnityEvent onOverride = new UnityEvent();
        public ThrowDataContainer()
        {
            curves = new FrisbeeCurve();
            stopOnWallCollision = true;
            macroName = "";
        }
        public ThrowDataContainer(string name, bool stopColl)
        {
            macroName = name;stopOnWallCollision = stopColl;
            curves = new FrisbeeCurve();
        }
        #region Helpers
        public ThrowDataContainer AdjustThrowSpeed(float factor)
        {
            ScaleSpeed(Vector3.one * factor);
            return this;
        }
        /// <summary>
        /// Get the total length in seconds of the special.
        /// </summary>
        /// <returns></returns>
        public float TotalLength()
        {
            return Mathf.Max(curves.GetLengthX(), curves.GetLengthY(), curves.GetLengthZ());
        }
        /// <summary>
        /// Scale the speed of this throw data.
        /// </summary>
        /// <param name="factor">What to multiply x, y, and z values to.</param>
        public void ScaleSpeed(Vector3 factor)
        {
            for(int i = 0; i < curves.x.keys.Length;i++)
            {
                Keyframe kf = curves.x.keys[i];
                kf.value *= factor.x;
                curves.x.MoveKey(i, kf);
            }
            for (int i = 0; i < curves.y.keys.Length; i++)
            {
                Keyframe kf = curves.y.keys[i];
                kf.value *= factor.y;
                curves.y.MoveKey(i, kf);
            }
            for (int i = 0; i < curves.z.keys.Length; i++)
            {
                Keyframe kf = curves.z.keys[i];
                kf.value *= factor.z;
                curves.z.MoveKey(i, kf);
            }
        }
        /// <summary>
        /// Normalizes the values of the curves to a min and max value of -1 and 1, respectively.
        /// </summary>
        public void NormalizeSpeed()
        {
            Vector3 d = curves.GetSpdMax(true, true, true);
            if (d.x == 0) { d.x = 1; }
            if (d.y == 0) { d.y = 1; }
            if (d.z == 0) { d.z = 1; }
            d.x = Mathf.Abs(1 / d.x);
            d.y = Mathf.Abs(1 / d.y);
            d.z = Mathf.Abs(1 / d.z);
            ScaleSpeed(d);
        }
        /// <summary>
        /// Get X WrapMode
        /// </summary>
        public WrapMode WrapModeX()
        {return curves.x.postWrapMode;}
        /// <summary>
        /// Get Y WrapMode;
        /// </summary>
        public WrapMode WrapModeY()
        {return curves.y.postWrapMode;}
        /// <summary>
        /// Get Z WrapMode
        /// </summary>
        public WrapMode WrapModeZ()
        { return curves.z.postWrapMode; }
        #endregion
    }
    [System.Serializable]
    public class FrisbeeCurve
    {
        public AnimationCurve x;
        public AnimationCurve y;
        public AnimationCurve z;

        public FrisbeeCurve()
        {
            x = new AnimationCurve();
            y = new AnimationCurve();
            z = new AnimationCurve();
        }

        #region Helpers
        /// <summary>
        /// Add a new key to every curve in this FrisbeeCurve.
        /// </summary>
        /// <param name="xx">The Keyframe to add to x</param>
        /// <param name="yy">The Keyframe to add to y</param>
        /// <param name="zz">The Keyframe to add to z</param>
        public void AddKey(Keyframe xx, Keyframe yy,Keyframe zz)
        {
            x.AddKey(xx);y.AddKey(yy);z.AddKey(zz);
        }
        /// <summary>
        /// Get the highest value of each curve.
        /// </summary>
        /// <returns>Returned as Vector3, parses all 3 curves.</returns>
        public Vector3 GetSpdMax(bool xx = true, bool yy = true, bool zz = true)
        {
            Vector3 result = Vector3.one * int.MinValue;
            if (xx) foreach (Keyframe kf in x.keys) { if (result.x < kf.value) { result.x = kf.value; } }
            if (yy) foreach (Keyframe kf in y.keys) { if (result.y < kf.value) { result.y = kf.value; } }
            if (zz) foreach (Keyframe kf in z.keys) { if (result.z < kf.value) { result.z = kf.value; } }
            if (result.x == int.MinValue) { result.x = 0; }
            if (result.y == int.MinValue) { result.y = 0; }
            if (result.z == int.MinValue) { result.z = 0; }
            return result;
        }
        /// <summary>
        /// Get the lowest value of each curve.
        /// </summary>
        /// <returns>Returned as Vector3, parses all 3 curves.</returns>
        public Vector3 GetSpdMin(bool xx = true, bool yy = true, bool zz = true)
        {
            Vector3 result = Vector3.one * int.MaxValue;
            if (xx) foreach (Keyframe kf in x.keys) { if (result.x > kf.value) { result.x = kf.value; } }
            if (yy) foreach (Keyframe kf in y.keys) { if (result.y > kf.value) { result.y = kf.value; } }
            if (zz) foreach (Keyframe kf in z.keys) { if (result.z > kf.value) { result.z = kf.value; } }
            if (result.x == int.MaxValue) { result.x = 0; }
            if (result.y == int.MaxValue) { result.y = 0; }
            if (result.z == int.MaxValue) { result.z = 0; }
            return result;
        }
        /// <summary>
        /// Set the preWrapMode of each or any curve.
        /// </summary>
        /// <param name="wm">The WrapMode to set the curves' preWrapMode to.</param>
        /// <param name="_x">Whether or not to set to x.</param>
        /// <param name="_y">Whether or not to set to y.</param>
        /// <param name="_z">Whether or not to set to z.</param>
        public void SetPreWrapMode(WrapMode wm, bool _x, bool _y, bool _z)
        {
            if (_x) { x.preWrapMode = wm; }
            if (_y) { y.preWrapMode = wm; }
            if (_z) { z.preWrapMode = wm; }
        }
        /// <summary>
        /// Set the postWrapMode of each or any curve.
        /// </summary>
        /// <param name="wm">The WrapMode to set the curves' postWrapMode to.</param>
        /// <param name="_x">Whether or not to set to x.</param>
        /// <param name="_y">Whether or not to set to y.</param>
        /// <param name="_z">Whether or not to set to z.</param>
        public void SetPostWrapMode(WrapMode wm, bool _x, bool _y, bool _z)
        {
            if (_x) { x.postWrapMode = wm; }
            if (_y) { y.postWrapMode = wm; }
            if (_z) { z.postWrapMode = wm; }
        }
        /// <summary>
        /// Get the time of the last keyframe in x.
        /// </summary>
        public float GetLengthX()
        {
            if (x.keys.Length < 1) { return 0; }
            return x.keys[x.keys.Length - 1].time;
        }
        /// <summary>
        /// Get the time of the last keyframe in y.
        /// </summary>
        public float GetLengthY()
        {
            if (y.keys.Length < 1) { return 0; }
            return y.keys[y.keys.Length - 1].time;
        }
        /// <summary>
        /// Get the time of the last keyframe in z.
        /// </summary>
        public float GetLengthZ()
        {
            if (z.keys.Length < 1) { return 0; }
            return z.keys[z.keys.Length - 1].time;
        }
        #endregion
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(PlayerMovement))]
	public class PlayerMovementEditor : Editor
	{
		#region Properties
		private SerializedProperty m_pID;
		private SerializedProperty m_pSide;

		private SerializedProperty m_catchDis;
		private SerializedProperty m_throwSpd;
        private SerializedProperty m_lobSpd;
		private SerializedProperty m_catchTransform;
        private SerializedProperty m_throwAngle;

		private SerializedProperty m_mSpd;
		private SerializedProperty m_mAcc;
		private SerializedProperty m_dSpd;
		private SerializedProperty m_dDAcc;

		private SerializedProperty m_macSet;
		private SerializedProperty m_specMoves;
        #endregion
        //angledThrowAngle
        #region AnimationCurve Properties
        private List<ThrowDataContainer> sThrows = new List<ThrowDataContainer>();
        private List<Vector3> sSpeedMax = new List<Vector3>();
        private List<Vector3> sSpeedMin = new List<Vector3>();
        private List<float> sTimeF = new List<float>();
        private List<bool> mFO = new List<bool>();
        #endregion

        private GUIStyle hLabel;
        private GUIStyle foBold;
        private GUIStyle miniHLabel;

        private PlayerMovement player;

		private void OnEnable()
		{
			m_pID = serializedObject.FindProperty("playerID");
			m_pSide = serializedObject.FindProperty("playerSide");

			m_catchDis = serializedObject.FindProperty("catchDistance");
			m_throwSpd = serializedObject.FindProperty("normalThrowSpeed");
            m_throwAngle = serializedObject.FindProperty("angledThrowAngle");
            m_lobSpd = serializedObject.FindProperty("normalLobSpeed");
			m_catchTransform = serializedObject.FindProperty("frisbeeCatchTransform");

			m_mSpd = serializedObject.FindProperty("moveSpeed");
			m_mAcc = serializedObject.FindProperty("moveAcceleration");
			m_dSpd = serializedObject.FindProperty("dashSpeed");
			m_dDAcc = serializedObject.FindProperty("dashDeceleration");

			m_macSet = serializedObject.FindProperty("macroSetName");
			m_specMoves = serializedObject.FindProperty("specialMoves");
            //Debug.Log(EditorStyles.centeredGreyMiniLabel.fontSize);

			LoadToCurve(true);
		}
		public override void OnInspectorGUI()
		{
            #region Setup GUIStyles
            if (hLabel == null)
            {
                hLabel = new GUIStyle();
                hLabel = EditorStyles.centeredGreyMiniLabel;
                hLabel.fontSize = 21;
                hLabel.fontStyle = FontStyle.BoldAndItalic;
                hLabel.font.material.color = Color.black;

                foBold = EditorStyles.foldoutHeader;
                foBold.fontStyle = FontStyle.Bold;

                miniHLabel = new GUIStyle();
                miniHLabel = EditorStyles.centeredGreyMiniLabel;
                miniHLabel.fontSize = 16;
                miniHLabel.fontStyle = FontStyle.Bold;
                miniHLabel.font.material.color = Color.black;
            }
            #endregion
            serializedObject.Update();
            #region Identification
            GUILayout.Label("Identification",hLabel);
            m_pID.intValue = EditorGUILayout.IntField("Player ID", m_pID.intValue);
            m_pSide.intValue = EditorGUILayout.IntSlider("Side", m_pSide.intValue, 1, -1);
            if (m_pSide.intValue == 0) { m_pSide.intValue = 1; }
            #endregion
            #region Movement Stats
            GUILayout.Label("Movement Stats",hLabel);
            m_mSpd.floatValue = EditorGUILayout.FloatField("Movement Speed", m_mSpd.floatValue);
            m_mAcc.floatValue = EditorGUILayout.FloatField("Movement Acceleration", m_mAcc.floatValue);
            m_dSpd.floatValue = EditorGUILayout.FloatField("Dash Speed", m_dSpd.floatValue);
            m_dDAcc.floatValue = EditorGUILayout.FloatField("Dash Deceleration", m_dDAcc.floatValue);
            #endregion
            #region Frisbee Parameters
            GUILayout.Label("Frisbee Parameters",hLabel);
            m_catchTransform.objectReferenceValue = EditorGUILayout.ObjectField("Catch Transform", m_catchTransform.objectReferenceValue,typeof(GameObject),true);
            m_catchDis.floatValue = EditorGUILayout.FloatField("Catch Distance", m_catchDis.floatValue);
            m_throwSpd.floatValue = EditorGUILayout.FloatField("Throw Speed", m_throwSpd.floatValue);
            m_lobSpd.floatValue = EditorGUILayout.FloatField("Lob Speed", m_lobSpd.floatValue);
            m_throwAngle.floatValue = EditorGUILayout.Slider("Max Throw Angle", m_throwAngle.floatValue, 0, 89);
            #endregion
            #region Special Moves
            GUILayout.Label("Special Moves",hLabel);
            m_macSet.stringValue = EditorGUILayout.TextField("Macro Set Name", m_macSet.stringValue);
            LoadToCurve();
            SizeLogic(EditorGUILayout.DelayedIntField("# of Special Moves", sThrows.Count));
            for (int i = 0; i < sThrows.Count;i++)
            {
                mFO[i] = EditorGUILayout.Foldout(mFO[i], sThrows[i].macroName, true,foBold);
                if (mFO[i])
                {
                    sThrows[i].ID = EditorGUILayout.IntField("Throw ID", sThrows[i].ID);
                    sThrows[i].macroName = EditorGUILayout.TextField("Macro Name", sThrows[i].macroName);
                    sThrows[i].meterUsage = EditorGUILayout.Slider("Meter Usage (Percentage)",sThrows[i].meterUsage, 0, 1);
                    sThrows[i].noHoldReq = EditorGUILayout.Toggle("Only allow while not held", sThrows[i].noHoldReq);
                    sThrows[i].overrideCurves = EditorGUILayout.Toggle("Override Curves With Event", sThrows[i].overrideCurves);
                    if (!sThrows[i].overrideCurves)
                    { 
                        GUILayout.Label("Z (Tangential) Speed",miniHLabel);
                        EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.BeginVertical();
                                sSpeedMax[i] = new Vector3(sSpeedMax[i].x, sSpeedMax[i].y,EditorGUILayout.FloatField("Max Spd",sSpeedMax[i].z));
                                sSpeedMin[i] = new Vector3(sSpeedMin[i].x, sSpeedMin[i].y,EditorGUILayout.FloatField("Min Spd",sSpeedMin[i].z));
                            EditorGUILayout.EndVertical();
                            sThrows[i].curves.z = EditorGUILayout.CurveField(sThrows[i].curves.z, Color.blue, new Rect(new Vector2(0, sSpeedMin[i].z), new Vector2(sTimeF[i], Mathf.Abs(sSpeedMax[i].z) + Mathf.Abs(sSpeedMin[i].z))));
                        EditorGUILayout.EndHorizontal();
                        GUILayout.Label("X (Forward) Speed", miniHLabel);
                        EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.BeginVertical();
                                sSpeedMax[i] = new Vector3(EditorGUILayout.FloatField("Max Spd",sSpeedMax[i].x), sSpeedMax[i].y, sSpeedMax[i].z);
                                sSpeedMin[i] = new Vector3(EditorGUILayout.FloatField("Min Spd",sSpeedMin[i].x), sSpeedMin[i].y, sSpeedMin[i].z);
                            EditorGUILayout.EndVertical();                            
                                sThrows[i].curves.x = EditorGUILayout.CurveField(sThrows[i].curves.x, Color.red, new Rect(new Vector2(0, sSpeedMin[i].x), new Vector2(sTimeF[i],Mathf.Abs(sSpeedMax[i].x)+Mathf.Abs(sSpeedMin[i].x))));
                        EditorGUILayout.EndHorizontal();
                        GUILayout.Label("Y (Upward) Speed", miniHLabel);
                        EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.BeginVertical();
                                sSpeedMax[i] = new Vector3(sSpeedMax[i].x, EditorGUILayout.FloatField("Max Spd", sSpeedMax[i].y), sSpeedMax[i].z);
                                sSpeedMin[i] = new Vector3(sSpeedMin[i].x, EditorGUILayout.FloatField("Min Spd", sSpeedMin[i].y), sSpeedMin[i].z);
                            EditorGUILayout.EndVertical();                            
                                sThrows[i].curves.y = EditorGUILayout.CurveField(sThrows[i].curves.y, Color.green, new Rect(new Vector2(0, sSpeedMin[i].y), new Vector2(sTimeF[i],Mathf.Abs(sSpeedMax[i].y)+Mathf.Abs(sSpeedMin[i].y))));
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(m_specMoves.GetArrayElementAtIndex(i).FindPropertyRelative("onOverride"));
                    }
                    GUILayout.Space(5);
                    float lt = sTimeF[i];
                    sTimeF[i] = EditorGUILayout.DelayedFloatField("Time Length", sTimeF[i]);
                    //SCALE EXISTING KEYS TO MATCH NEW TIME SO WE DONT HAVE TO REDO THEM//
                    if (lt != sTimeF[i])
                    {
                        for (int p = 0;p<sThrows[i].curves.x.keys.Length;p++)
                        {
                            Keyframe kf = sThrows[i].curves.x.keys[p];
                            kf.time *= (sTimeF[i] / lt);
                            sThrows[i].curves.x.MoveKey(p, kf);
                        }
                        for (int p = 0; p < sThrows[i].curves.y.keys.Length; p++)
                        {
                            Keyframe kf = sThrows[i].curves.y.keys[p];
                            kf.time *= (sTimeF[i] / lt);
                            sThrows[i].curves.y.MoveKey(p, kf);
                        }
                        for (int p = 0; p < sThrows[i].curves.z.keys.Length; p++)
                        {
                            Keyframe kf = sThrows[i].curves.z.keys[p];
                            kf.time *= (sTimeF[i] / lt);
                            sThrows[i].curves.z.MoveKey(p, kf);
                        }
                    }
                    GUILayout.Space(5);
                    sThrows[i].stopOnWallCollision = EditorGUILayout.Toggle("Stop on wall collision", sThrows[i].stopOnWallCollision);
                    sThrows[i].overrideSpeedCap = EditorGUILayout.Toggle("Override Speed Cap", sThrows[i].overrideSpeedCap);
                    sThrows[i].forceKnockdown = EditorGUILayout.Toggle("Force Knockdown Opponent", sThrows[i].forceKnockdown);
                    GUILayout.Space(10);
                }
            }
            SaveFromCurve();
            #endregion
            serializedObject.ApplyModifiedProperties();
		}
        private void LoadToCurve(bool init = false)
		{
            if (player == null)
            { player = ((PlayerMovement)serializedObject.targetObject); }//.GetComponent<PlayerMovement>(); }
            if (player != null)
            {
                ThrowDataContainer[] tdc = player.LoadThrows();
                if (tdc != null)
                {sThrows = tdc.ToList();}
                else
                { sThrows = new List<ThrowDataContainer>(); }
                if (init)
                {
                    Vector3 maxx = Vector3.zero;
                    Vector3 minn = Vector3.zero;
                    for (int i = 0; i < sThrows.Count; i++)
                    {
                        mFO.Add(false);
                        maxx = sThrows[i].curves.GetSpdMax();
                        minn = sThrows[i].curves.GetSpdMin();

                        maxx.x = Mathf.Max(0,Mathf.Ceil(maxx.x));
                        maxx.y = Mathf.Max(0, Mathf.Ceil(maxx.y));
                        maxx.z = Mathf.Max(0, Mathf.Ceil(maxx.z));

                        minn.x = Mathf.Min(0, Mathf.Ceil(minn.x));
                        minn.y = Mathf.Min(0, Mathf.Ceil(minn.y));
                        minn.z = Mathf.Min(0, Mathf.Ceil(minn.z));

                        sSpeedMax.Add(maxx);
                        sSpeedMin.Add(minn);
                        sTimeF.Add(sThrows[i].TotalLength());
                    }
                }
            }

            #region I forgot I could just do ^
            /* for (int i = 0; i < m_specMoves.arraySize;i++)
            {
                sThrows.Add(new ThrowDataContainer());
                sThrows[i].macroName = m_specMoves.GetArrayElementAtIndex(i).FindPropertyRelative("macroName").stringValue;
                sThrows[i].stopOnWallCollision = m_specMoves.GetArrayElementAtIndex(i).FindPropertyRelative("stopOnWallCollision").boolValue;
                sThrows[i].curves = new FrisbeeCurve();
                var cx = m_specMoves.GetArrayElementAtIndex(i).FindPropertyRelative("curves").FindPropertyRelative("x");
                var cy = m_specMoves.GetArrayElementAtIndex(i).FindPropertyRelative("curves").FindPropertyRelative("y");
                var cz = m_specMoves.GetArrayElementAtIndex(i).FindPropertyRelative("curves").FindPropertyRelative("z");

                Keyframe kf;
                kf.

                sThrows[i].curves.x = new AnimationCurve();
                sThrows[i].curves.x.preWrapMode = (WrapMode)cx.FindPropertyRelative("preWrapMode").enumValueIndex;
                sThrows[i].curves.x.postWrapMode = (WrapMode)cx.FindPropertyRelative("postWrapMode").enumValueIndex;
                for (int o = 0; o < cx.FindPropertyRelative("keys").arraySize;o++)
                {
                    var _k = cx.FindPropertyRelative("keys").GetArrayElementAtIndex(o);
                    sThrows[i].
                    sThrows[i].curves.x.AddKey(_k.FindPropertyRelative(")
                }
                /*var cop = m_specMoves.GetArrayElementAtIndex(i).Copy();
                var it = cop.GetEnumerator();
                sThrows.Add(new ThrowDataContainer());
                while (cop.NextVisible(true))
                {
                    Debug.Log(cop.name);
                }*/
            // }
            /*for (int i = 0; i < m_specMoves.arraySize;i++)
            {
                SerializedProperty _sp = m_specMoves.GetArrayElementAtIndex(i).FindPropertyRelative("physicsInfo");
                mNames.Add(m_specMoves.GetArrayElementAtIndex(i).FindPropertyRelative("macroName").stringValue);
                mFO.Add(false);

                sStopCol.Add(_sp.FindPropertyRelative("stopOnWallCollision").boolValue);
                sMoveCurvesX.Add(n
                sMoveCurvesZ.Add(new AnimationCurve());
                float time = 0;
                for (int o = 0; o <_sp.arraySize;o++)
                {
                    sMoveCurvesX[i].AddKey(time, _sp.GetArrayElementAtIndex(o).FindPropertyRelative("velocity").vector3Value.x);
                    sMoveCurvesX[i].
                    time += _sp.GetArrayElementAtIndex(o).FindPropertyRelative("timer").floatValue;
                }
                sTimeF.Add(time);
            }*/
            #endregion
        }
		private void SaveFromCurve()
		{
            player.SaveThrows(sThrows.ToArray());
        }
        private void SizeLogic(int cc)
        {
            while (sThrows.Count < cc)
            {
                sThrows.Add(new ThrowDataContainer());
                sSpeedMax.Add(Vector3.one);
                sSpeedMin.Add(-Vector3.one);
                sTimeF.Add(1);
                mFO.Add(true);
            }
            while (sThrows.Count > cc)
            {
                sThrows.RemoveAt(sThrows.Count - 1);
                sTimeF.RemoveAt(sTimeF.Count - 1);
                sSpeedMin.RemoveAt(sSpeedMin.Count - 1);
                sSpeedMax.RemoveAt(sSpeedMax.Count - 1);
                mFO.RemoveAt(mFO.Count - 1);
            }

        }
    }
	#endif
}