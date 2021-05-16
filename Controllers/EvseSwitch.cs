using System.Net;
using System.IO;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Text;
public class EvseSwitch {

    public enum SwitchStatus { SwitchOK, SwtichFAIL }

    public class SwitchResponse {
        public string msg { get; set; }
        public SwitchStatus status { get; set; }
    }

    public interface ISwitch {
        public SwitchResponse SwitchOn();
        public SwitchResponse SwitchOff();
        public bool IsOn();
    }

    public class ConsolePlug : ISwitch {
        public string id;

        public bool isPlugOn = false;

        public SwitchResponse SwitchOn () {
            if (isPlugOn) {
                return new SwitchResponse () {
                msg = DateTime.Now + ": console Evse " + id + " already on.",
                status = SwitchStatus.SwtichFAIL
                };
            }
            Thread.Sleep(3453);
            isPlugOn = true;
            return new SwitchResponse () {
                msg = DateTime.Now + ": console Evse " + id + " switched on.",
                status = SwitchStatus.SwitchOK
            };
        }
        public SwitchResponse SwitchOff () {
            Thread.Sleep(2453);
            isPlugOn = false;
            return new SwitchResponse () {
                msg = DateTime.Now + ": console Evse " + id + " switched off.",
                status = SwitchStatus.SwitchOK
            };
        }

        public bool IsOn() { return true; }
    }

    public class SimpleHttpPlug : ISwitch {
         public string url { get; set; }
         public string on { get; set; }
         public string off { get; set; }
         private SwitchResponse get(string cmd) {
            try {
                if (IsOn()) {
                    return new SwitchResponse() {
                        msg ="Evse already on",
                        status = SwitchStatus.SwtichFAIL
                    };
                } else {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(cmd);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    response.Close();
                    return new SwitchResponse() {
                        msg = "",
                        status = SwitchStatus.SwitchOK
                    };
                }
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
                return new SwitchResponse() {
                    msg = e.ToString(),
                    status = SwitchStatus.SwtichFAIL
                };
            }
        }
        public SwitchResponse SwitchOn() { return get(url + on); }
        public SwitchResponse SwitchOff() { return get(url + off); }
        public bool IsOn () { return true; }
    }

    public class IPPower9255Pro : ISwitch {
        public string url { get; set; }
        public string port { get; set; }
        public string user { get; set; }
        public string pwd { get; set; }

        private SwitchResponse exec(string cmd) {
            try {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(cmd);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                response.Close();
                return new SwitchResponse() {
                    msg = "",
                    status = SwitchStatus.SwitchOK
                };
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
                return new SwitchResponse() {
                    msg = e.ToString(),
                    status = SwitchStatus.SwtichFAIL
                };
            }
        }

        private string GetUrlPrefix () { return url + ":" + port + "/set.cmd?user=" + user + "+pass=" + pwd + "+cmd="; }
        public SwitchResponse SwitchOn() {
            if (IsOn()) {
                return new SwitchResponse() {
                    msg ="Evse already on",
                    status = SwitchStatus.SwtichFAIL
                };
            } else {
                return exec(GetUrlPrefix() + "setpower&p61=1");
            }
        }
        public SwitchResponse SwitchOff() { return exec(GetUrlPrefix() + "setpower&p61=0"); }

        public bool IsOn () {
            try {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(GetUrlPrefix() + "getpower");
                using (var response = (HttpWebResponse)request.GetResponse()) {
                    var encoding = Encoding.GetEncoding(response.CharacterSet);
                    using (var responseStream = response.GetResponseStream())
                    using (var reader = new StreamReader(responseStream, encoding)) {
                        string msg = reader.ReadToEnd();
                        response.Close();
                        return msg.Contains("p61=1");
                    }
                }
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

    }

    public static Dictionary<string,ISwitch> Switches = new Dictionary<string, ISwitch>() {
        { "WNCooL", new ConsolePlug() { id = "EVSE000001" } },
        { "EVSE000003", new ConsolePlug() { id = "EVSE000003" } },
        { "EVSE000002", new IPPower9255Pro() { url = "http://82.65.82.158", port="40281", user="admin", pwd="12345678" } },
        { "EVSE000004", new SimpleHttpPlug() { url = "http://3.250.1.82/bulb_", on ="on", off="off" } },
    };

}