/*
 * Created by SharpDevelop.
 * User: David
 * Date: 2/14/2018
 * Time: 12:59 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;

namespace NebliDex
{
	/// <summary>
	/// Interaction logic for SeedList.xaml
	/// </summary>
	public partial class SeedList : Window
	{
		public SeedList(string dns)
		{
			InitializeComponent();
			WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
			if(App.DNS_SEED_TYPE == 0){
				this.dns_field.Text = dns;
			}else{
				this.ip_field.Text = dns;
			}
			
			if(App.main_window != null){
				Connect_Button.Content = "Update";
			}
		}
		
		private void Reset_DNS(object sender, RoutedEventArgs e)
		{
			this.dns_field.Text = App.Default_DNS_SEED;
		}
		
		private async void Close_Dialog(object sender, RoutedEventArgs e)
		{
			if(this.ip_field.Text.Trim().Length > 0){
				App.DNS_SEED_TYPE = 1; //IP Address
				App.DNS_SEED = this.ip_field.Text.Trim();
			}else{
				App.DNS_SEED = this.dns_field.Text.Trim();
				App.DNS_SEED_TYPE = 0; //Http address
			}
			Connect_Button.IsEnabled = false; //Disable the button
			Connect_Button.Content = Connect_Button.Content+"...";
			
			if(App.main_window != null){ //Only delete if we are trying to update the CN list
				File.Delete(App.App_Path+"/data/cn_list.dat");
			}

			await Task.Run(() => App.FindCNServers(true) ); //Get the Nodes

			this.Close();
		}
	}
}