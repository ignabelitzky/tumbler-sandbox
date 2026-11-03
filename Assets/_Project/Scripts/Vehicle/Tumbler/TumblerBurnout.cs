using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Latched tire spin: entry at rest, continuation while both directional inputs are held.</summary>
public sealed class TumblerBurnout
{
    private readonly WheelCollider[] front, rear;
    private readonly WheelCollider[] wheels;
    private readonly WheelFrictionCurve[] forward, sideways;
    private bool releaseRequired;
    public bool Active { get; private set; }
    public TumblerBurnout(WheelCollider[] front, WheelCollider[] rear)
    {
        this.front=front;this.rear=rear;
        wheels=new WheelCollider[front.Length+rear.Length];front.CopyTo(wheels,0);rear.CopyTo(wheels,front.Length);
        forward=new WheelFrictionCurve[wheels.Length];sideways=new WheelFrictionCurve[wheels.Length];
        for(int i=0;i<wheels.Length;i++){forward[i]=wheels[i].forwardFriction;sideways[i]=wheels[i].sidewaysFriction;}
    }
    public static bool BothDirectionsHeld()
    {
        var k=Keyboard.current;
        if(k!=null&&(k.wKey.isPressed||k.upArrowKey.isPressed)&&(k.sKey.isPressed||k.downArrowKey.isPressed))return true;
        foreach(var pad in Gamepad.all)if(pad.rightTrigger.ReadValue()>.5f&&pad.leftTrigger.ReadValue()>.5f)return true;
        return false;
    }
    public void UpdateState(bool held,Rigidbody body,bool grounded)
    {
        if(!held){releaseRequired=false;Active=false;return;}
        if(!Active&&!releaseRequired&&grounded&&body.linearVelocity.sqrMagnitude<.0004f&&body.angularVelocity.sqrMagnitude<.0004f)Active=true;
        // Once airborne, require a fresh combination on the ground rather than reigniting on landing.
        if(Active&&!grounded)Cancel();
    }
    public bool IsDriven(WheelCollider wheel,TumblerController.driveType mode)
    {
        return (mode!=TumblerController.driveType.RearWheelDrive&&System.Array.IndexOf(front,wheel)>=0)||
            (mode!=TumblerController.driveType.FrontWheelDrive&&System.Array.IndexOf(rear,wheel)>=0);
    }
    public bool IsBurning(WheelCollider wheel,TumblerController.driveType mode)=>Active&&IsDriven(wheel,mode)&&wheel.GetGroundHit(out _);
    public void Apply(TumblerController.driveType mode,float steer)
    {
        for(int i=0;i<wheels.Length;i++)
        {
            var wheel=wheels[i];bool burning=IsBurning(wheel,mode);
            var f=wheel.forwardFriction;var s=wheel.sidewaysFriction;
            f.extremumValue=forward[i].extremumValue*(burning?.30f:1f);f.asymptoteValue=forward[i].asymptoteValue*(burning?.25f:1f);
            bool frontAxle=System.Array.IndexOf(front,wheel)>=0;
            s.extremumValue=sideways[i].extremumValue*(burning?(frontAxle?.65f:.12f):1f);s.asymptoteValue=sideways[i].asymptoteValue*(burning?(frontAxle?.5f:.1f):1f);
            if(Active&&!frontAxle&&!IsDriven(wheel,mode)&&wheel.isGrounded)
            {
                float pivotGrip=Mathf.Lerp(1f,.15f,Mathf.Abs(steer));
                s.extremumValue=sideways[i].extremumValue*pivotGrip;s.asymptoteValue=sideways[i].asymptoteValue*pivotGrip;
            }
            wheel.forwardFriction=f;wheel.sidewaysFriction=s;
            if(!Active)continue;
            if(!IsDriven(wheel,mode))wheel.motorTorque=0;
            // Stationary tires resist creep; releasing their hold with steering permits a physical turn.
            wheel.brakeTorque=IsDriven(wheel,mode)?0:Mathf.Lerp(7000,0,Mathf.Abs(steer));
        }
    }
    public void Cancel(){Active=false;releaseRequired=true;}
}
