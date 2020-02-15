using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using DiscGame.Gameplay.UI;
using DiscGame.Dialog;

namespace DiscGame.Gameplay
{
    public class FrisbeePhysics : MonoBehaviour
    {
        #region INTERNAL REFERENCES
        public GameObject lobIndicator;
        private Renderer pr;
        private Renderer pRender { get { pr = pr ?? GetComponent<Renderer>(); return pr; } }
        private Rigidbody rb;
        private Rigidbody rbSelf { get { rb = rb ?? GetComponent<Rigidbody>(); return rb; } }
        private Collider cl;
        private Collider colSelf { get { cl = cl ?? GetComponent<Collider>(); return cl; } }
        private Score scr;
        private Score score { get { scr = scr ?? GetComponent<Score>(); return scr; } }
        private PlayerSoundManager sm;
        private PlayerSoundManager pSM { get { sm = sm ?? GetComponent<PlayerSoundManager>(); return sm; } }
        #endregion
        #region EXTERNAL REFERENCES
        private PlayerMovement[] plyrs;
        public PlayerMovement[] players { get { plyrs = plyrs ?? GameObject.FindObjectsOfType<PlayerMovement>(); return plyrs; } }
        private MatchTimer mt;
        private MatchTimer matchtimer { get { mt = mt ?? FindObjectOfType<MatchTimer>(); return mt; } }

        private BoxCollider pf;
        private BoxCollider playField { get { pf = pf ?? GameObject.FindGameObjectWithTag("PlayField").GetComponent<BoxCollider>(); return pf; } }

        [SerializeField]
        GameObject centerStageDivider;
        public GameObject CenterStageDivider { get { return centerStageDivider; } }
        #endregion
        #region DYNAMIC REFERENCES
        private GameObject struckWall;
        [HideInInspector]
        public GameObject catchTransform;
        [HideInInspector]
        public GameObject prevCatchTransform;
        [HideInInspector]
        public string lastThrownBy;
        #endregion

        #region THROWING
        [SerializeField]
        float catchCooldownTime;

        //WHETHER THE FRISBEE IS BEING LOBBED OR THROWN, IT CANNOT GO SLOWER THAN THIS VALUE//
        [SerializeField]
        private float minSpeed;
        //Limit the frisbees speed to this value.
        [SerializeField]
        private float speedCap;
        private float lastSpeedMult;
        private Vector3 lastVelocity;
        [HideInInspector]
        public float speedMultiplier;

        private int zDirection = 1;

        public ThrowDataContainer movementData;
        //X, Y, and Z movement data have different timers for different WrapModes.
        private Vector3 timer;
        public bool inSpecial = false;
        public bool useThrowData = false;
        #endregion

        #region LOBBING
        private Vector3 lobFrom;
        public int isLobbingToX = -1;
        private int isLobbingToZ = -1;
        private bool isCurrentlyLobbing { get { return (isLobbingToX != -1); } }
        [Header("See tooltip")]
        [Tooltip("0-2 = Player 1's lob goal positions, 3-5 = Player 2's lob goal positions. In world space. All 6 must be defined.")]
        public float[] lobPosX;
        public float[] lobPosZ;
        Vector2 tempLob;
        private float defaultY;
        private float lobHeight;
        private bool reachedHeight;
        private float lobScale;
        #endregion


        public bool inKelsSpecial = false;

        void Start()
        {
            rbSelf.velocity = -Vector3.right * 20;
            movementData = null;useThrowData = false;
            //SET DEFAULT Y POSITION THAT SHOULD BE MAINTAINED WHILE IN THROWS//
            defaultY = transform.position.y;
            lobHeight = transform.position.y + 6;
            //SET DEFAULT LOB INDICATOR SCALE//
            if (lobIndicator != null) { lobScale = lobIndicator.transform.localScale.magnitude; }
            //RANDOMLY TOSS TO ONE OF THE PLAYERS//
            if (Random.value > .5f) { score.returntoPlayer2(); }
            else { score.returntoPlayer1(); }
            //MAKE THIS AUDIO SOURCE AUDIBLE BUT STILL SPACIAL//
            pSM.asSelf.spatialBlend = .2f;
            pSM.asSelf.minDistance = 25;

        }
        
        void FixedUpdate()
        {
            // FOR ANOTHER, THEORETICAL DAY.//
            /*if (inKelsSpecial)
            {
                colSelf.enabled = false;
            }*/
            if (GameManager.bHasWon)
            { gameObject.SetActive(false); }
            colSelf.enabled = true;
            bool changeColor = rbSelf.velocity.magnitude >= speedCap;
            if (!changeColor && movementData != null)
            {
                if (movementData.forceKnockdown) { changeColor = true; }
            }
            if (changeColor)
            {
                pRender.sharedMaterial.SetColor("_EmissionColor", Color.red);
            }
            else
            {
                pRender.sharedMaterial.SetColor("_EmissionColor", new Color(.77f, .77f, .77f));
            }
            if (useThrowData && movementData!=null)
            {
                //TIMER LOGIC//
                int _cancel = 0;
                if ((movementData.WrapModeX() == WrapMode.Loop || movementData.WrapModeX() == WrapMode.PingPong || timer.x < movementData.curves.GetLengthX()))
                { timer.x += Time.fixedDeltaTime * speedMultiplier; }
                else { timer.x = movementData.curves.GetLengthX(); _cancel += 1; }
                if ((movementData.WrapModeY() == WrapMode.Loop || movementData.WrapModeY() == WrapMode.PingPong || timer.y < movementData.curves.GetLengthY()))
                { timer.y += Time.fixedDeltaTime*speedMultiplier; }
                else { timer.y = movementData.curves.GetLengthY(); _cancel += 1; }
                if ((movementData.WrapModeZ() == WrapMode.Loop || movementData.WrapModeZ() == WrapMode.PingPong || timer.z < movementData.curves.GetLengthZ()))
                { timer.z += Time.fixedDeltaTime*speedMultiplier; }
                else { timer.z = movementData.curves.GetLengthZ(); _cancel += 1; }

                if (_cancel==3)
                { CancelMoveData(); }
                else
                {
                    //SET VELOCITY//
                    //TO DO (MAYBE): INTERPRET CURVES AS POSITION INSTEAD OF VELOCITY?//
                    rbSelf.velocity = new Vector3(movementData.curves.x.Evaluate(timer.x),
                                                  0,//movementData.curves.y.Evaluate(timer.y),
                                                  movementData.curves.z.Evaluate(timer.z)*zDirection)
                                                   * speedMultiplier;
                    if (rbSelf.velocity.magnitude > speedCap && ! movementData.overrideSpeedCap)
                    {
                        rbSelf.velocity = rbSelf.velocity.normalized * speedCap;
                    }
                   /* if (rbSelf.velocity.x > speedCap && !movementData.overrideSpeedCap) //magnitude > speedCap && ! movementData.overrideSpeedCap)
                    {
                        //rbSelf.velocity = rbSelf.velocity.normalized * speedCap;
                        rbSelf.velocity = Vector3.Scale(rbSelf.velocity, new Vector3(0, 1, 1)) + (Vector3.right * Mathf.Clamp(rbSelf.velocity.x, -speedCap, speedCap));
                    }
                    if (rbSelf.velocity.z > speedCap && !movementData.overrideSpeedCap) //magnitude > speedCap && ! movementData.overrideSpeedCap)
                    {
                        //rbSelf.velocity = rbSelf.velocity.normalized * speedCap;
                        rbSelf.velocity = Vector3.Scale(rbSelf.velocity, new Vector3(1, 1, 0)) + (Vector3.forward * Mathf.Clamp(rbSelf.velocity.z, -speedCap, speedCap));
                    }*/
                    rbSelf.velocity = rbSelf.velocity.normalized * Mathf.Max(rbSelf.velocity.magnitude, minSpeed);
                }
                transform.position = Vector3.Scale(new Vector3(1, 0, 1), transform.position) + (Vector3.up * defaultY);
            }
            else if (isLobbingToX != -1)
            {
                colSelf.enabled = false;
                float xgoal = tempLob.x;
                if (isLobbingToX != -2 && isLobbingToX != -3)
                {
                    xgoal = lobPosX[isLobbingToX];
                }
                if (Mathf.InverseLerp(lobFrom.x, xgoal, transform.position.x) <= .5f && isLobbingToX != -3)
                {
                    rbSelf.velocity = new Vector3(Mathf.Abs(rbSelf.velocity.x) * -Mathf.Sign(transform.position.x - xgoal), Mathf.Abs(rbSelf.velocity.x), rbSelf.velocity.z);
                    if (Mathf.Abs(defaultY - transform.position.y) >= lobHeight)
                    {
                        rbSelf.velocity = new Vector3(rbSelf.velocity.x, 0, rbSelf.velocity.z);
                        transform.position = new Vector3(transform.position.x, defaultY + lobHeight, transform.position.z);
                    }
                    
                }               
                if (Mathf.InverseLerp(lobFrom.x, xgoal, transform.position.x) >= .995f || isLobbingToX == -3)
                {
                    rbSelf.velocity = new Vector3(0, Mathf.Min(rbSelf.velocity.y,0) - .5f, 0);
                    colSelf.enabled = true;
                }
                else
                {
                    rbSelf.velocity = rbSelf.velocity.normalized * Mathf.Max(rbSelf.velocity.magnitude, minSpeed);
                }
                if (isLobbingToZ != -1)
                {
                    transform.position = new Vector3(transform.position.x, transform.position.y,
                        Mathf.Lerp(lobFrom.z, lobPosZ[isLobbingToZ], Mathf.InverseLerp(lobFrom.x, xgoal, transform.position.x)));
                }
                transform.position = Vector3.Scale(transform.position, new Vector3(1, 0, 1)) + Vector3.up * Mathf.Min(defaultY + lobHeight, transform.position.y);
                if (lobIndicator != null)
                {
                    float zz = transform.position.z;
                    if (isLobbingToZ != -1) { zz = lobPosZ[isLobbingToZ]; }
                    lobIndicator.transform.position = new Vector3(xgoal, lobIndicator.transform.position.y, zz);
                    lobIndicator.SetActive(true);
                    lobIndicator.transform.localScale = Vector3.Lerp(Vector3.one*lobScale, Vector3.one*lobScale*.25f, Mathf.InverseLerp(lobIndicator.transform.position.y, lobIndicator.transform.position.y + lobHeight, transform.position.y));
                }
            }
            else
            {
                rbSelf.velocity = Vector3.Scale(rbSelf.velocity, new Vector3(1, 0, 1));
            }
            if (isLobbingToX == -1 && lobIndicator != null)
            {
                lobIndicator.SetActive(false);
            }
            if (catchTransform != null)
            {
                inKelsSpecial = false;
                timer = Vector3.zero;
                gameObject.transform.position = catchTransform.transform.position;
                gameObject.transform.rotation = catchTransform.transform.rotation;
                //Debug.Log(catchTransform.transform.position);
            }
            else { gameObject.transform.rotation = Quaternion.identity; }

            for (int i = 0; i < players.Length; i++)
            {
                if (Vector3.Distance(players[i].transform.position, transform.position) <= players[i].CatchDistance &&
                    (prevCatchTransform != players[i].FrisbeeCatchTransform || Mathf.Max(timer.x,timer.y,timer.z) > catchCooldownTime))
                {
                    CatchFrisbee(players[i]);
                }
            }
            
            if (struckWall != null)
            {
                if (Vector3.Distance(transform.position,struckWall.transform.position) <= colSelf.bounds.extents.magnitude*1.3f)// struckWall.colSelf.bounds.Intersects(colSelf.bounds))
                {
                    struckWall.layer = 10;
                }
                else
                {
                    struckWall.layer = 0;
                    struckWall = null;
                }
            }
            else if (!isCurrentlyLobbing && rbSelf.velocity.x != 0)
            {
                lastVelocity = rbSelf.velocity;
            }
        }
        /// <summary>
        /// Clear current throw data.
        /// </summary>
        public void CancelMoveData()
        {
            timer = Vector3.zero;
            movementData = null;
            inSpecial = false;
            useThrowData = false;
            tempLob = new Vector2(transform.position.x,transform.position.z);
            isLobbingToX = -1;
            isLobbingToZ = -1;
            transform.position = new Vector3(transform.position.x, defaultY, transform.position.z);
            zDirection = 1;
        }
        public void SetVelocity(Vector3 v)
        {
            rbSelf.velocity = v;zDirection = 1;
        }
        public Vector3 GetVelocity() { return rbSelf.velocity; }
        /// <summary>
        /// Resets both catchTransform and prevCatchTransform.
        /// </summary>
        public void ResetOwner()
        {
            catchTransform = null;
            prevCatchTransform = null;
            zDirection = 1;
        }
        void CatchFrisbee(PlayerMovement in_Player)
        {
            zDirection = 1;
            this.GetComponent<Score>().disableScores();
            this.GetComponent<SmearEffect>().checkScore(true);

            Vector2 kb = new Vector2(rbSelf.velocity.x, rbSelf.velocity.z)*.05f;
           
            if (in_Player.FrisbeeCatchTransform != prevCatchTransform && !in_Player.aniSelf.GetBool("knockedDown"))
            {
                if (!GameManager.bHasWon)
                { GameManager.bStopTimer = false; }
                bool dont = false;
                if (movementData != null)
                {
                    if ((movementData.forceKnockdown || rbSelf.velocity.magnitude >= speedCap)
                        && isLobbingToX == -1 && isLobbingToZ == -1)
                    {
                        if (in_Player.canParry)
                        {
                            //catchTransform = prevCatchTransform;
                            in_Player.SetAniTrigger("Parry");
                            Vector3 spd = rbSelf.velocity;
                            timer = Vector3.right;
                            CancelMoveData();
                            rbSelf.velocity = Vector3.zero;
                            LobFrisbee(spd, -3, 1,true);
                            tempLob = new Vector2(transform.position.x, transform.position.z);
                            //Debug.Log("FrisbeeParried");
                            transform.position = Vector3.Scale(transform.position, new Vector3(1, 0, 1)) + (Vector3.up *lobHeight);
                        }
                        else
                        {
                            matchtimer.gameObject.GetComponent<PlayerSoundManager>().PlaySound("SFX_InGame_StruckFoul01(Updated)");
                            //catchTransform = prevCatchTransform;
                            in_Player.SetAniBool("knockedDown", true);
                            int xx = FindFarthestLobPosition(in_Player.transform.position, in_Player.playerID);
                            Vector3 spd = rbSelf.velocity/speedMultiplier;
                            speedMultiplier = 1.0f;
                            spd.z *= -1;
                            timer = Vector3.zero;
                            CancelMoveData();
                            in_Player.currentKBSpd = kb;
                            rbSelf.velocity = Vector3.zero;
                            LobFrisbee(spd, xx, 1,true);
                            //Debug.Log("Frisbee knock down!"+spd);
                        }
                        dont = true;
                    }
                }
                if (!dont)
                {
                    if (in_Player.noCatchWindow == 0 && in_Player.aniSelf.GetBool("canCatch"))
                    {
                        in_Player.noCatchWindow = in_Player.maxNoCatchWindow;
                        if (isLobbingToX != -1) { in_Player.SetAniTrigger("catchLobbedFrisbee"); }
                        else { in_Player.SetAniTrigger("catchFrisbee"); }
                        prevCatchTransform = catchTransform;
                        catchTransform = in_Player.FrisbeeCatchTransform;
                        in_Player.gameObject.GetComponentInChildren<PlayerSoundManager>().PlaySound("SFX_InGame_DiscCatch");
                        //Debug.Log("FRISBEECAUGHT");
                        timer = Vector3.zero;
                        CancelMoveData();
                        in_Player.currentKBSpd = kb;
                        rbSelf.velocity = Vector3.zero;
                    }
                }
                
            }
        }
        private int FindFarthestLobPosition(Vector3 playerPos, int playerID)
        {
            int result = 0;
            int[] indices = new int[] { 0, 1, 2 };
            if (playerID == 0) { indices = new int[] { 3, 4, 5 }; result = 3; }
            float record = -Mathf.Infinity;
            for (int i = 0; i < indices.Length;i++)
            {
                if (Vector3.Distance(playerPos,new Vector3(lobPosX[indices[i]],playerPos.y,playerPos.z))> record)
                {
                    record = Vector3.Distance(playerPos, new Vector3(lobPosX[indices[i]], playerPos.y, playerPos.z));
                    result = indices[i];
                }
            }
            return result;
        }

        public void ThrowFrisbee(ThrowDataContainer moveData,bool isSpecial, float powerMult)
        {            
            if (catchTransform != null)
            {
                lastThrownBy = catchTransform.transform.root.GetComponent<NPC>().dialogue.name;
                if (powerMult > 1) { speedMultiplier += powerMult; }
                else { speedMultiplier = powerMult; }
                speedMultiplier = Mathf.Min(speedMultiplier, 2);
                if (moveData.macroName == "Super")
                {
                    speedMultiplier = Mathf.Min(speedMultiplier, 1.3f);
                }
                lastSpeedMult = powerMult;
                prevCatchTransform = catchTransform;
                catchTransform.transform.root.GetComponentInChildren<PlayerSoundManager>().PlaySound("SFX_InGame_DiscThrow");
                if (isSpecial && moveData.macroName == "Super")
                {
                    CheckMyName(lastThrownBy, catchTransform.transform.root.gameObject);
                }
                catchTransform = null;
                timer = Vector3.zero;
                inSpecial = isSpecial;
                movementData = moveData;
                useThrowData = true;                
                //Debug.Log("THROW");
            }
        }
        public void LobFrisbee(Vector3 speed, int position, float powerMult, bool overrideCatchTransform = false)
        {
            if (catchTransform != null || overrideCatchTransform)
            {
                if (powerMult > 1) { speedMultiplier = Mathf.Max(1,speedMultiplier+((powerMult/speedMultiplier)*.45f)); }
                else { speedMultiplier = powerMult; }
                speedMultiplier = Mathf.Min(speedMultiplier, 2);
                lastSpeedMult = powerMult;

                if (!overrideCatchTransform)
                {
                    lastThrownBy = catchTransform.transform.root.GetComponent<NPC>().dialogue.name;
                    prevCatchTransform = catchTransform;
                    catchTransform = null;
                }
                timer = Vector3.zero;
                inSpecial = false;
                movementData = null;
                useThrowData = false;

                if (position != -1)
                {
                    lobFrom = transform.position;
                    isLobbingToX = position;
                    if (speed.z > 0) { isLobbingToZ = 1; }
                    if (speed.z < 0) { isLobbingToZ = 0; }
                }
                else
                {
                    isLobbingToX = -2;
                    isLobbingToZ = -2;
                    lobFrom = Vector3.zero;
                }

                rbSelf.velocity = new Vector3(Mathf.Sign(speed.x)*Mathf.Min(Mathf.Abs(speed.x*Mathf.Max(1,speedMultiplier)),speedCap),0,0);
                //Debug.Log("LOB WITH"+rbSelf.velocity);
                
            }
        }

        private Vector3 CalculateLobPosition(Vector3 spd)
        {
            Vector3 result = transform.position + (spd);
            result = playField.ClosestPointOnBounds(result);
            result.y = result.z;
            return result;
        }

        private void OnCollisionEnter(Collision collision)
        {
            
            if (catchTransform == null && !isCurrentlyLobbing)
            {
                if (collision.gameObject.tag == "Wall")
                {
                    if (score.goals.Where((ScoreInfo si) => (si.goalCollider == collision.gameObject)).ToArray().Length <= 0)
                    {
                        //BOUNCE
                        if (Mathf.Sign(Vector3.Reflect(lastVelocity.normalized, collision.GetContact(0).normal).x) == Mathf.Sign(lastVelocity.normalized.x))
                        {
                            prevCatchTransform = null;
                            if (useThrowData && movementData != null)
                            {
                                //Cancel throw in favor of traditional bounce logic (TBD)
                                if (movementData.stopOnWallCollision)
                                { CancelMoveData(); zDirection = 1; }
                                //Assume top/bottom wall bounce (for now), reverse keyframe values and continue throw data.
                                else
                                {
                                    zDirection *= -1;
                                }
                                
                            }pSM.PlayRandomSound("SFX_InGame_DiscRicochetandImpact",1f,.05f,.8f,.4f);
                            rbSelf.velocity = Vector3.Scale(rbSelf.velocity, new Vector3(1, 0, 1));
                        }
                        //GO INTO LOB//
                        else
                        {

                            zDirection = 1;
                            //if (prevCatchTransform != null){
                            //prevCatchTransform = null;
                           // catchTransform = prevCatchTransform; //}
                            Vector3 spd = lastVelocity;
                            timer = Vector3.right;
                            CancelMoveData();
                            rbSelf.velocity = Vector3.zero;
                            LobFrisbee(spd, -2, 1, true);
                            if (playField == null)
                            {
                                tempLob = new Vector2(transform.position.x, transform.position.z);
                            }
                            else
                            {
                                tempLob = CalculateLobPosition(spd);
                            }
                            pSM.PlayRandomSound("SFX_InGame_DiscRicochetandImpact", 1f, .05f, .8f, .4f);
                            //Debug.Log("Frisbee Head-On Collision");
                            struckWall = collision.collider.gameObject;
                            struckWall.layer = 10;
                        }
                    }
                }
            }
            
        }

        private void CheckMyName(string myName, GameObject player)
        {
            //Debug.Log(myName + "Using Special.");
            PlayerSoundManager pSM = player.GetComponentInChildren<PlayerSoundManager>();
            if (pSM != null)
            {
                /*if (myName == "C.H.A.D.")
                { pSM.PlaySound("SFX_Character_CHADSpecialRelease"); }
                else if (myName == "Maxwell")
                { pSM.PlaySound("SFX_Character_MaxwellSpecialRelease"); }
                else if (myName == "Ariela")
                { pSM.PlaySound("SFX_Character_ArielaSpecialRelease"); }
                else if (myName == "Kels")
                { pSM.PlaySound("SFX_Character_MichaelaSpecialRelease"); }*/
            }
            else {
                //Debug.LogWarning("NO PLAYERSOUNDMANAGER FOUND");
            }
        }
    }
}