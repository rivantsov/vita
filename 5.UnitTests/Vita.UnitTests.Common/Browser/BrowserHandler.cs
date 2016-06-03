using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Windows.Forms;

namespace Vita.UnitTests.Common {

  public class BrowserHandler {

    public BrowserHandler() {
    }

    public BrowserForm Form  {
      get { return _form; }
    } BrowserForm _form; 

    public void Open(bool visible = true) {
      _form = null; 
      var thread = new Thread(StartWinFormsApp);
      thread.SetApartmentState(ApartmentState.STA);
      thread.Start();
      while(_form == null)
        Thread.Sleep(10);
      Application.DoEvents();
      if (visible)
        InvokeAction(() => _form.Show());
      Application.DoEvents();
    }

    public void NavigateTo(string url, bool wait = true) {
      InvokeAction(() => {
        _form.Browser.Navigate(url);
        if(wait)
          while(_form.Browser.ReadyState != WebBrowserReadyState.Complete)
            Application.DoEvents();
      });
    }// 

    public void Wait() {
      InvokeFunc(() => {
        while(_form.Browser.ReadyState != WebBrowserReadyState.Complete)
          Application.DoEvents();
        return true; 
      });
    }

    public void Click(HtmlElement elem, bool wait = true) {
      elem.InvokeMember("click");
      if(wait)
        Wait(); 
    }

    public HtmlDocument GetDocument() {
      return InvokeFunc(() => _form.Browser.Document);
    }

    public void Close() {
      InvokeAction(() => _form.Close());
      Application.Exit();
    }


    // Utiliites =========================================================
    private void StartWinFormsApp() {
      _form = new BrowserForm();
      Application.Run(_form);
    }

    class Box<T> {
      public T Value;
    }

    private T InvokeFunc<T>(Func<T> func) {
      var box = new Box<T>();
      InvokeAction(() => box.Value = func());
      return box.Value;
    }

    private void InvokeAction(Action action) {
      _form.Invoke(action);
      Application.DoEvents();
    }

  } //class
}
