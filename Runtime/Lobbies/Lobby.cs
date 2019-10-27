using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HouraiTeahouse.Networking  {

public interface IMetadataContainer {

  string GetMetadata(string key);
  void SetMetadata(string key, string value);
  void DeleteMetadata(string key);

  // Returns -1 if the container does not support iteration.
  int GetMetadataCount();
  // Returns null if the container does not support iteration.
  string GetKeyByIndex(int idx);

}

public abstract class Lobby : INetworkSender, IMetadataContainer, IDisposable {

  public abstract ulong Id { get; }
  public abstract LobbyType Type { get; }
  public abstract ulong OwnerId { get; }
  public abstract ulong UserId { get; }
  public abstract uint Capacity { get; }
  public virtual bool IsLocked => false;

  public ConnectionStats ConnectionStats {
    get {
      var stats = new ConnectionStats();
      foreach (var member in Members) {
        stats += member.ConnectionStats;
      }
      return stats;
    }
  }

  public event Action<LobbyMember> OnMemberJoin {
    add => Members.OnMemberJoin += value;
    remove => Members.OnMemberJoin -= value;
  }
  public event Action<LobbyMember> OnMemberLeave {
    add => Members.OnMemberLeave += value;
    remove => Members.OnMemberLeave -= value;
  }

  public event Action OnUpdated;
  public event Action OnDeleted;
  public event Action<LobbyMember, byte[], uint> OnLobbyMessage;
  public event Action<LobbyMember, byte[], uint> OnNetworkMessage;
  public event Action<LobbyMember> OnMemberUpdated;

  public abstract int MemberCount { get; }
  protected abstract ulong GetMemberId(int idx);

  public LobbyMemberMap Members { get; }

  protected Lobby() {
    Members = new LobbyMemberMap(this);
    OnMemberJoin += (member) => {
      member.OnNetworkMessage += (buf, size) => {
        OnNetworkMessage?.Invoke(member, buf, size);
      };

      member.OnUpdated += () => OnMemberUpdated?.Invoke(member);
    };
    OnMemberLeave += (member) => member.DispatchDisconnect();
    var count = MemberCount;
    for (int i = 0; i < count; i++) {
      Members.Get(GetMemberId(i));
    }
  }

  public bool IsJoinable => !IsLocked && MemberCount < Capacity;

  public abstract Task Join();
  public abstract void Leave();
  public abstract void Delete();

  public abstract string GetMetadata(string key);
  public abstract void SetMetadata(string key, string value);
  public abstract void DeleteMetadata(string key);

  public abstract int GetMetadataCount();
  public abstract string GetKeyByIndex(int idx);

  public virtual string GetMemberMetadata(AccountHandle handle, string key) =>
    throw new NotSupportedException();
  public virtual void SetMemberMetadata(AccountHandle handle, string key, string value) =>
    throw new NotSupportedException();
  public virtual void DeleteMemberMetadata(AccountHandle handle, string key) =>
    throw new NotSupportedException();

  public virtual int GetMemberMetadataCount(AccountHandle handle) =>
    throw new NotSupportedException();
  public virtual string GetMemberMetadataKey(AccountHandle handle, int idx) =>
    throw new NotSupportedException();

  public abstract void SendLobbyMessage(byte[] msg, int size = -1);

  internal abstract void SendNetworkMessage(AccountHandle handle, byte[] msg, int size = -1,
                                          Reliability reliability = Reliability.Reliable);

  internal void DispatchDelete() => OnDeleted?.Invoke();
  internal void DispatchLobbyMessage(LobbyMember handle, byte[] msg, uint size) =>
    OnLobbyMessage?.Invoke(handle, msg, size);
  internal void DispatchUpdate() => OnUpdated?.Invoke();

  void INetworkSender.SendMessage(byte[] msg, int size, Reliability reliability) =>
    SendLobbyMessage(msg, size);

  public virtual void Dispose() {
    Members.Dispose();
    OnUpdated = null;
    OnDeleted = null;
    OnNetworkMessage = null;
    OnMemberUpdated = null;
  }

}

public static class IMetadataContainerExtensions {

  public static Dictionary<string, string> GetAllMetadata(this IMetadataContainer container) {
    var count = container.GetMetadataCount();
    if (count < 0) {
      throw new ArgumentException("Metadata container does not support iteration");
    }
    var output = new Dictionary<string, string>();
    for (var i = 0; i < count; i++) {
      string key =  container.GetKeyByIndex(i);
      output[key] = container.GetMetadata(key);
    }
    return output;
  }

}

}