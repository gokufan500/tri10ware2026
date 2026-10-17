using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    [DefaultExecutionOrder(-250), DisallowMultipleComponent]
    public sealed class TutorialSequence : MonoBehaviour
    {
        [Header("Shared scene systems")]
        [FormerlySerializedAs("player")]
        public PlayerMotor Player;
        [FormerlySerializedAs("sceneTransition")]
        public SceneTransition SceneTransition;
        [FormerlySerializedAs("followCamera")]
        public FollowCamera2D FollowCamera;
        [FormerlySerializedAs("levelFlow")]
        public LevelFlow LevelFlow;
        [FormerlySerializedAs("autoStart")]
        public bool AutoStart = true;
        [Header("First lesson")]
        [FormerlySerializedAs("stageOne")]
        public StageOneTutorial StageOne;
        [Tooltip("Geometry, gates and anchors for the first room. Keep the shared player and systems outside it.")]
        [FormerlySerializedAs("stageOneRoot")]
        public GameObject StageOneRoot;
        [FormerlySerializedAs("stageOneSpawn")]
        public Transform StageOneSpawn;
        [FormerlySerializedAs("stageOneBounds")]
        public CameraBounds2D StageOneBounds;
        [FormerlySerializedAs("stageOneCameraSettings")]
        public CameraSettings StageOneCameraSettings;
        [FormerlySerializedAs("stageOneKillY")]
        public float StageOneKillY = -25f;
        [Header("Second lesson and practice room")]
        [FormerlySerializedAs("stageTwo")]
        public StageTwoTutorial StageTwo;
        [Tooltip("Geometry and anchors for the enclosed practice room. Starts inactive.")]
        [FormerlySerializedAs("stageTwoRoot")]
        public GameObject StageTwoRoot;
        [FormerlySerializedAs("stageTwoSpawn")]
        public Transform StageTwoSpawn;
        [FormerlySerializedAs("stageTwoBounds")]
        public CameraBounds2D StageTwoBounds;
        [FormerlySerializedAs("stageTwoCameraSettings")]
        public CameraSettings StageTwoCameraSettings;
        [FormerlySerializedAs("stageTwoKillY")]
        public float StageTwoKillY = -5f;
        private bool _changeRequested;
        public enum ActiveStage
        {
            StageOne,
            StageTwo
        }

        public ActiveStage CurrentStage
        {
            get;
            private set;
        } = ActiveStage.StageOne;
        public bool IsChangingStage => _changeRequested || (SceneTransition && SceneTransition.IsTransitioning);

        private void Start()
        {
            if (AutoStart)
            {
                BeginTutorial();
            }
        }

        private void Update()
        {
            if (_changeRequested && SceneTransition && !SceneTransition.IsTransitioning)
            {
                _changeRequested = false;
            }
        }

        public void BeginTutorial()
        {
            if (!ReferencesReady())
            {
                return;
            }
            StageOne.AutoStart = StageTwo.AutoStart = false;
            ShowRoom(ActiveStage.StageOne);
        }

        public void EnterStageTwo()
        {
            if (_changeRequested || CurrentStage == ActiveStage.StageTwo || !ReferencesReady())
            {
                return;
            }

            _changeRequested = true;

            SceneTransition.FadeWithinScene(() => ShowRoom(ActiveStage.StageTwo), false);
        }

        public void RestartStageTwo()
        {
            if (_changeRequested || !ReferencesReady())
            {
                return;
            }

            _changeRequested = true;
            SceneTransition.FadeWithinScene(() => ShowRoom(ActiveStage.StageTwo));
        }

        private bool ReferencesReady()
        {
            if (Player && SceneTransition && FollowCamera && LevelFlow && StageOne && StageTwo && StageOneRoot && StageTwoRoot && StageOneRoot != StageTwoRoot && StageOneSpawn && StageTwoSpawn && StageOneBounds && StageTwoBounds && StageOneCameraSettings && StageTwoCameraSettings)
            {
                return true;
            }

            Debug.LogError("Missing connection", this);
            return false;
        }

        private void ShowRoom(ActiveStage next)
        {
            StageOne.enabled = false;
            StageTwo.enabled = false;
            bool first = next == ActiveStage.StageOne;
            StageOneRoot.SetActive(first);
            StageTwoRoot.SetActive(!first);
            CurrentStage = next;
            Transform spawn = first ? StageOneSpawn : StageTwoSpawn;
            LevelFlow.Player = Player;
            LevelFlow.SpawnPoint = spawn;
            LevelFlow.FollowCamera = FollowCamera;
            LevelFlow.KillY = first ? StageOneKillY : StageTwoKillY;
            Player.ResetAt(spawn.position);
            Physics2D.SyncTransforms();
            FollowCamera.Target = Player.transform;
            FollowCamera.Bounds = first ? StageOneBounds : StageTwoBounds;
            FollowCamera.Settings = first ? StageOneCameraSettings : StageTwoCameraSettings;
            FollowCamera.SnapToTarget();
            if (first)
            {
                StageOne.Sequence = this;
                StageOne.enabled = true;
                StageOne.BeginTutorial();
            }
            else
            {
                StageTwo.Sequence = this;
                StageTwo.enabled = true;
                StageTwo.BeginTutorial();
            }
        }
    }
}
