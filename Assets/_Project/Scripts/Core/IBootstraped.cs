
public interface IBootstraped {
    public bool isInitialised { get;  set; }
    public void Init();

    public object GetRegistrInst() {
        return this;
    }
}