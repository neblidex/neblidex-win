/*
 * Created by SharpDevelop.
 * User: David
 * Date: 5/23/2018
 * Time: 9:28 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace NebliDex
{
	/// <summary>
	/// Interaction logic for UserPrompt.xaml
	/// </summary>
	public partial class UserPrompt : Window
	{
		public string final_response="";
		private string backup="";
		private int tries=-1;
		bool is_password=true;
		
		public UserPrompt(string ques,bool password)
		{
			InitializeComponent();
			WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
			Prompt.Text = ques;
			is_password = password;
			if(is_password == false){
				tries=0;
				User_Response_Pass.Visibility = Visibility.Collapsed;
				User_Response.Visibility = Visibility.Visible;
			}
		}
		
		private void Save_Info(object sender, RoutedEventArgs e)
		{
			string response="";
			if(is_password == true){
				response = User_Response_Pass.Password;
			}else{
				response = User_Response.Text;
			}
			
			response = response.Trim();
			
			if(response.Length < 6 && response.Length > 0){
				MessageBox.Show("This password is too short. Please make it at least 6 characters.");
			}else{
				if(tries == 0 && response.Length > 0){
					//We want the user to re-enter the password
					MessageBox.Show("For confirmation, please re-enter previously entered password. Do not lose this password. There is no option to recover it!");
					Prompt.Text = "Please re-enter the password\nfor confirmation.";
					backup = response;
					User_Response.Text = "";
					tries++;
					return;
				}else if(tries == 1){
					if(response.Equals(backup) == false){
						MessageBox.Show("The password doesn't match the previously entered.");
						return; //For the user to try again
					}
				}
				final_response = response;
				this.Close();
			}
		}
	}
}