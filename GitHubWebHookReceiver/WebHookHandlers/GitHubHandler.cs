using LibGit2Sharp;
using Microsoft.AspNet.WebHooks;
using Microsoft.Web.Administration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace GitHubWebHookReceiver.WebHookHandlers
{
    public class GitHubHandler : WebHookHandler
    {
        public override Task ExecuteAsync(string receiver, WebHookHandlerContext context)
        {
            var site_name = "GitHubPRs";
            var site_port = 8083;
            var app_pool_name = "GitHubWebHookReceiver";
            var app_pool_username = "Administrator";
            var app_pool_password = "Pa$$w0rd";
            var site_base_path = Path.Combine("C:\\", "Users", "Administrator", "Desktop", "sites", site_name);

            var data = context.GetDataOrDefault<JObject>();
            var action = data["action"].Value<string>();
            var prNumber = data["number"].Value<string>();

            var repo = data["repository"];
            var repo_name = repo["name"].Value<string>();
            var clone_url = repo["clone_url"].Value<string>();

            var pr = data["pull_request"];
            var pr_head = pr["head"];
            var pr_head_ref = pr_head["ref"].Value<string>();

            var pr_links = pr["_links"];
            var pr_links_self = pr_links["self"];
            var pr_links_self_href = pr_links_self["href"];

            var appPath = string.Format("/{0}/{1}", repo_name, prNumber);
            var pr_checkout_path = Path.Combine(site_base_path, repo_name, prNumber);

            using (var serverManager = new ServerManager())
            {
                var appPool = serverManager.ApplicationPools.SingleOrDefault(a => a.Name == site_name);
                if (appPool == null)
                {
                    appPool = serverManager.ApplicationPools.Add(app_pool_name);
                    appPool.ManagedRuntimeVersion = "v4.0";
                    appPool.ProcessModel.IdentityType = ProcessModelIdentityType.SpecificUser;
                    appPool.ProcessModel.UserName = app_pool_username;
                    appPool.ProcessModel.Password = app_pool_password;
                }

                var site = serverManager.Sites.SingleOrDefault(s => s.Name == site_name);
                if (site == null)
                {
                    Directory.CreateDirectory(site_base_path);
                    site = serverManager.Sites.Add(
                        site_name,
                        site_base_path,
                        site_port);

                    site.Applications[0].ApplicationPoolName = app_pool_name;
                }

                if (!Directory.Exists(pr_checkout_path) && action == "opened")
                {
                    Repository.Clone(
                        clone_url,
                        pr_checkout_path,
                        new CloneOptions { BranchName = pr_head_ref });
                }
                if(Directory.Exists(pr_checkout_path) && action != "opened")
                {
                    Directory.Delete(pr_checkout_path);
                }

                var app = site.Applications.SingleOrDefault(a => a.Path == appPath);
                if (app == null && action == "opened")
                {
                    app = site.Applications.Add(appPath, pr_checkout_path);
                    app.ApplicationPoolName = app_pool_name;
                    var customerVirtualDirectory = app.VirtualDirectories.Single(v => v.Path == "Customer");
                    if (customerVirtualDirectory == null)
                    {
                        customerVirtualDirectory = app.VirtualDirectories.Add("Customer", Path.Combine(pr_checkout_path, "Customer"));
                    }
                    var employeeVirtualDirectory = app.VirtualDirectories.Single(v => v.Path == "Employee");
                    if (employeeVirtualDirectory == null)
                    {
                        employeeVirtualDirectory = app.VirtualDirectories.Add("Employee", Path.Combine(pr_checkout_path, "Employee"));
                    }
                }
                if (app != null && action != "opened")
                {
                    site.Applications.Remove(app);
                }

                serverManager.CommitChanges();
            }

            executeRakeTask("sayhello", pr_checkout_path);

            return Task.FromResult(true);
        }

        private void executeRakeTask(string taskName, string workingDirectory)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "C:\\Ruby23-x64\\bin\\rake.bat",
                WorkingDirectory = workingDirectory,
                Arguments = taskName,
                CreateNoWindow = true,
            });
            process.WaitForExit(120000);
        }
    }
}