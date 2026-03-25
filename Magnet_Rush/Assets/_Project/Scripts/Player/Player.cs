using UnityEngine;
using MagnetRush.Common;

namespace MagnetRush.Player
{
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerEvents))]
    public class Player : Entity.Entity
    {
        [SerializeField] private PlayerSettings settings;

        public PlayerInputHandler input { get; private set; }
        public PlayerEvents events { get; private set; }
        public PlayerStateManager states { get; private set; }
        public PlayerSettings Settings => settings;
        public Magnetizable magnetizable { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            input = GetComponent<PlayerInputHandler>();
            events = GetComponent<PlayerEvents>();
            states = GetComponent<PlayerStateManager>();
            magnetizable = GetComponent<Magnetizable>();

            // HP=0でDiePlayerStateに遷移
            if (health != null)
            {
                health.OnDie += OnDie;
            }
        }

        void OnDestroy()
        {
            if (health != null)
            {
                health.OnDie -= OnDie;
            }
        }

        private void OnDie()
        {
            states.Change<States.DiePlayerState>();
        }

        void Update()
        {
            if (states.current != null)
            {
                states.current.Step(Time.deltaTime);
            }

            ApplyGravity(settings.gravity, settings.snapForce, Time.deltaTime);
            ApplyMovement(Time.deltaTime);
        }

        public void MoveWithInput(float dt)
        {
            Vector3 dir = GetCameraRelativeDirection(input.MoveInput);
            if (dir.sqrMagnitude > 0.01f)
            {
                Accelerate(dir, settings.acceleration, settings.topSpeed, dt);
                FaceDirection(dir, settings.rotationSpeed, dt);
            }
        }

        public void MoveWithInputStrafe(float dt)
        {
            Vector3 dir = GetCameraRelativeDirection(input.MoveInput);
            float aimSpeed = settings.topSpeed * settings.aimMoveSpeedMultiplier;
            if (dir.sqrMagnitude > 0.01f)
            {
                Accelerate(dir, settings.acceleration, aimSpeed, dt);
            }
            if (cachedCameraTransform != null)
            {
                Vector3 camForward = cachedCameraTransform.forward;
                camForward.y = 0f;
                FaceDirection(camForward, settings.rotationSpeed * 2f, dt);
            }
        }

        public void SlowDown(float dt)
        {
            Decelerate(settings.deceleration, dt);
        }
    }
}
