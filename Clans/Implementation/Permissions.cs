using System.ComponentModel;

namespace Clans {
  public static class Permission {
    [Description("For usage of /clan")]
    public static readonly string Use = "clans.use";

    [Description("For usage of /c")]
    public static readonly string Chat = "clans.chat";

    [Description("For usage of /clan create")]
    public static readonly string Create = "clans.create";

    [Description("For usage of /clan reloadclans & /clan reloadconfig")]
    public static readonly string Reload = "clans.reload";
  }
}
